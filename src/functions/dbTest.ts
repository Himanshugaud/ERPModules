import { app, HttpRequest, HttpResponseInit, InvocationContext } from "@azure/functions";
import { getPool } from "../db";

export async function dbTest(
    request: HttpRequest,
    context: InvocationContext
): Promise<HttpResponseInit> {
    context.log("Database connectivity test requested.");

    try {
        const pool = await getPool();
        await pool.request().query("SELECT 1 AS connected");

        return {
            jsonBody: {
                connected: true
            }
        };
    } catch {
        context.error("Database connectivity check failed.");

        return {
            status: 500,
            jsonBody: {
                connected: false,
                error: "Database connectivity check failed.",
                traceId: context.invocationId
            }
        };
    }
}

app.http("dbTest", {
    route: "db-test",
    methods: ["GET"],
    authLevel: "function",
    handler: dbTest
});
