import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { test } from "node:test";
import { runInNewContext } from "node:vm";
import ts from "typescript";

const apiSource = readFileSync(new URL("../src/api/client.ts", import.meta.url), "utf8");
const apiOutput = ts.transpileModule(apiSource, {
  compilerOptions: { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.CommonJS }
}).outputText.replaceAll("import.meta", "({})");
const authSource = readFileSync(new URL("../src/auth/AuthContext.tsx", import.meta.url), "utf8");
const authParsed = ts.createSourceFile("AuthContext.tsx", authSource, ts.ScriptTarget.Latest, true, ts.ScriptKind.TSX);
const provider = authParsed.statements.find((statement) => ts.isFunctionDeclaration(statement) && statement.name?.text === "AuthProvider");
const providerOutput = ts.transpileModule(provider.getText(authParsed), {
  compilerOptions: { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.CommonJS, jsx: ts.JsxEmit.React }
}).outputText;

function token(expiresAt, extra = {}) {
  return `header.${Buffer.from(JSON.stringify({ exp: expiresAt, ...extra })).toString("base64url")}.signature`;
}

function session(fetchResponse = async () => new Response("{}")) {
  const storage = new Map();
  const localStorage = {
    getItem: (key) => storage.get(key) ?? null,
    setItem: (key, value) => storage.set(key, value),
    removeItem: (key) => storage.delete(key)
  };
  const window = new EventTarget();
  const exports = {};
  const clock = { now: Date.now() };
  const context = {
    exports, localStorage, window, Event, FormData, atob, URLSearchParams,
    Date: { now: () => clock.now },
    fetch: fetchResponse
  };
  runInNewContext(apiOutput, context);
  return { ...exports, context, storage, clock };
}

test("only unexpired, well-formed tokens restore authentication", () => {
  const auth = session();
  const future = Math.floor(auth.clock.now / 1000) + 3600;
  const valid = token(future);
  auth.setToken(valid);
  assert.equal(auth.getToken(), valid);
  for (const invalid of [token(0), token(Math.floor(auth.clock.now / 1000)), "broken", "header.e30.signature", token("tomorrow")]) {
    auth.setToken(invalid);
    assert.equal(auth.getToken(), null);
  }
});

test("401 clears the current token and notifies the auth provider even for a non-JSON response", async () => {
  const auth = session(async () => new Response("Unauthorized", { status: 401 }));
  auth.setToken(token(Math.floor(auth.clock.now / 1000) + 3600));
  let expired = 0;
  auth.context.window.addEventListener(auth.SESSION_EXPIRED_EVENT, () => expired++);
  await assert.rejects(auth.api.projects({}));
  assert.equal(auth.storage.has("erp_token"), false);
  assert.equal(expired, 1);
});

test("a delayed 401 from an older token cannot log out a newer session", async () => {
  let respond;
  const auth = session(() => new Promise((resolve) => { respond = resolve; }));
  const expiresAt = Math.floor(auth.clock.now / 1000) + 3600;
  auth.setToken(token(expiresAt, { sub: "old" }));
  const pending = auth.api.projects({});
  const fresh = token(expiresAt, { sub: "new" });
  auth.setToken(fresh);
  respond(new Response('{"error":{"message":"Expired"}}', { status: 401 }));
  await assert.rejects(pending);
  assert.equal(auth.getToken(), fresh);
});

for (const status of [403, 500]) {
  test(`${status} preserves authentication`, async () => {
    const auth = session(async () => new Response('{"error":{"message":"Failed"}}', { status }));
    const valid = token(Math.floor(auth.clock.now / 1000) + 3600);
    auth.setToken(valid);
    await assert.rejects(auth.api.projects({}), (error) => error.status === status);
    assert.equal(auth.getToken(), valid);
  });
}

function mountProvider(auth) {
  const effects = [];
  const timers = new Map();
  const state = { user: null, value: null };
  let timerId = 0;
  const render = runInNewContext(`${providerOutput}\nAuthProvider;`, {
    ...auth.context,
    ...auth,
    USER_KEY: "erp_user",
    AuthContext: { Provider: "Provider" },
    React: { createElement: (_type, props) => { state.value = props.value; } },
    useState: (initialize) => {
      state.user = initialize();
      return [state.user, (value) => { state.user = value; }];
    },
    useMemo: (factory) => factory(),
    useEffect: (effect) => effects.push(effect),
    prefetchReferenceData: () => {},
    document: new EventTarget(),
    setTimeout: (callback, delay) => { timers.set(++timerId, { callback, delay }); return timerId; },
    clearTimeout: (id) => timers.delete(id)
  });
  render({ children: null });
  const cleanup = effects.map((effect) => effect());
  return { state, timers, cleanup: () => cleanup.forEach((dispose) => dispose?.()) };
}

test("reload with an expired token removes the cached user and requires login", () => {
  const auth = session();
  auth.setToken(token(0));
  auth.storage.set("erp_user", JSON.stringify({ email: "user@example.test" }));
  const mounted = mountProvider(auth);
  assert.equal(mounted.state.value.isAuthenticated, false);
  assert.equal(mounted.state.user, null);
  assert.equal(auth.storage.has("erp_user"), false);
  assert.equal(auth.storage.has("erp_token"), false);
  mounted.cleanup();
});

test("an open session logs out when the expiry timer fires", () => {
  const auth = session();
  const expiresAt = Math.floor(auth.clock.now / 1000) + 60;
  auth.setToken(token(expiresAt));
  auth.storage.set("erp_user", JSON.stringify({ email: "user@example.test" }));
  const mounted = mountProvider(auth);
  assert.equal(mounted.state.value.isAuthenticated, true);
  const timer = [...mounted.timers.values()][0];
  assert.equal(timer.delay, expiresAt * 1000 - auth.clock.now);
  auth.clock.now = expiresAt * 1000;
  timer.callback();
  assert.equal(mounted.state.user, null);
  assert.equal(auth.storage.has("erp_user"), false);
  assert.equal(auth.storage.has("erp_token"), false);
  mounted.cleanup();
});

test("401 from the API clears the cached user through the provider", async () => {
  const auth = session(async () => new Response("{}", { status: 401 }));
  auth.setToken(token(Math.floor(auth.clock.now / 1000) + 3600));
  auth.storage.set("erp_user", JSON.stringify({ email: "user@example.test" }));
  const mounted = mountProvider(auth);
  await assert.rejects(auth.api.projects({}));
  assert.equal(mounted.state.user, null);
  assert.equal(auth.storage.has("erp_user"), false);
  mounted.cleanup();
});