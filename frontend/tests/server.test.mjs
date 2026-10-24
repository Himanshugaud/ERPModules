import assert from "node:assert/strict";
import { once } from "node:events";
import { mkdtemp, mkdir, writeFile, rm } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { before, after, test } from "node:test";
import { createApp } from "../server-app.js";

const shell = "<!doctype html><main>Synthetic ERP shell</main>";
let directory;
let server;
let baseUrl;

before(async () => {
  directory = await mkdtemp(join(tmpdir(), "erp-static-test-"));
  await mkdir(join(directory, "assets"));
  await writeFile(join(directory, "index.html"), shell);
  await writeFile(join(directory, "assets", "app.js"), "console.log('synthetic');");
  server = createApp(directory).listen(0, "127.0.0.1");
  await once(server, "listening");
  baseUrl = `http://127.0.0.1:${server.address().port}`;
});

after(async () => {
  if (server) await new Promise((resolve) => server.close(resolve));
  if (directory) await rm(directory, { recursive: true, force: true });
});

test("root serves the SPA document", async () => {
  const response = await fetch(`${baseUrl}/`);
  assert.equal(response.status, 200);
  assert.match(response.headers.get("content-type"), /^text\/html/);
  assert.equal(await response.text(), shell);
});

test("deep links and query strings preserve the SPA fallback", async () => {
  const response = await fetch(`${baseUrl}/projects/synthetic/tasks?view=active`);
  assert.equal(response.status, 200);
  assert.equal(await response.text(), shell);
});

test("HEAD deep links return document headers without a body", async () => {
  const response = await fetch(`${baseUrl}/projects/synthetic`, { method: "HEAD" });
  assert.equal(response.status, 200);
  assert.match(response.headers.get("content-type"), /^text\/html/);
  assert.equal(await response.text(), "");
});

test("static JavaScript assets are not replaced by the SPA document", async () => {
  const response = await fetch(`${baseUrl}/assets/app.js`);
  assert.equal(response.status, 200);
  assert.match(response.headers.get("content-type"), /^(text|application)\/javascript/);
  assert.equal(await response.text(), "console.log('synthetic');");
});

test("unsupported POST requests do not fall through to the SPA document", async () => {
  const response = await fetch(`${baseUrl}/projects/synthetic`, { method: "POST" });
  assert.equal(response.status, 404);
  assert.ok(!(await response.text()).includes(shell));
});