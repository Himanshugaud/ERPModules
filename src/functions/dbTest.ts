import { app, HttpRequest, HttpResponseInit, InvocationContext } from "@azure/functions";
import { getPool } from "../db";

export async function dbTest(
    request: HttpRequest,
    context: InvocationContext
): Promise<HttpResponseInit> {
    context.log(`DB connectivity test requested: ${request.method} ${request.url}`);

    try {
        const pool = await getPool();
        const result = await pool.request().query(
            "SELECT DB_NAME() AS databaseName, SUSER_SNAME() AS loginName, GETUTCDATE() AS serverTimeUtc"
        );

        return {
            jsonBody: {
                connected: true,
                info: result.recordset[0]
            }
        };
    } catch (err) {
        const message = err instanceof Error ? err.message : String(err);
        context.error(`Database connection failed: ${message}`);

        return {
            status: 500,
            jsonBody: {
                connected: false,
                error: message
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
