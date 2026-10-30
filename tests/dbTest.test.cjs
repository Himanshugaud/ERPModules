const assert = require("node:assert/strict");
const { test, mock } = require("node:test");
const { app } = require("@azure/functions");
const database = require("../dist/src/db");

let registration;
const register = mock.method(app, "http", (name, options) => {
    registration = { name, ...options };
});
const { dbTest } = require("../dist/src/functions/dbTest");
register.mock.restore();

function invocation() {
    const logs = [];
    return {
        logs,
        context: {
            invocationId: "synthetic-invocation",
            log: (...values) => logs.push(values),
            error: (...values) => logs.push(values)
        }
    };
}

const request = {
    method: "GET",
    url: "https://example.test/api/db-test?code=synthetic-function-key"
};

test("diagnostic keeps function-key protection", () => {
    assert.equal(registration.name, "dbTest");
    assert.equal(registration.route, "db-test");
    assert.equal(registration.authLevel, "function");
    assert.deepEqual(registration.methods, ["GET"]);
});

test("successful diagnostic exposes only connectivity", async (context) => {
    const queries = [];
    context.mock.method(database, "getPool", async () => ({
        request: () => ({
            query: async (query) => {
                queries.push(query);
                return { recordset: [{ databaseName: "synthetic-private-db", loginName: "synthetic-login" }] };
            }
        })
    }));
    const call = invocation();

    const response = await dbTest(request, call.context);

    assert.equal(response.status ?? 200, 200);
    assert.deepEqual(response.jsonBody, { connected: true });
    assert.deepEqual(queries, ["SELECT 1 AS connected"]);
});

test("request logging does not expose the URL or Functions key", async (context) => {
    context.mock.method(database, "getPool", async () => ({
        request: () => ({ query: async () => ({ recordset: [{}] }) })
    }));
    const call = invocation();

    await dbTest(request, call.context);

    assert.ok(!JSON.stringify(call.logs).includes("synthetic-function-key"));
    assert.ok(!JSON.stringify(call.logs).includes(request.url));
});

for (const stage of ["connect", "query"]) {
    for (const kind of ["error", "string"]) {
        test(`${stage} ${kind} failure does not disclose database details`, async (context) => {
            const detail = "synthetic-private-server password=synthetic-private-password";
            const failure = kind === "error" ? new Error(detail) : detail;
            context.mock.method(database, "getPool", async () => {
                if (stage === "connect") throw failure;
                return { request: () => ({ query: async () => { throw failure; } }) };
            });
            const call = invocation();

            const response = await dbTest(request, call.context);

            assert.equal(response.status, 500);
            assert.deepEqual(response.jsonBody, {
                connected: false,
                error: "Database connectivity check failed.",
                traceId: "synthetic-invocation"
            });
            assert.ok(!JSON.stringify(call.logs).includes(detail));
            assert.ok(!JSON.stringify(call.logs).includes("synthetic-function-key"));
        });
    }
}