import { app, HttpRequest, HttpResponseInit, InvocationContext } from "@azure/functions";

export async function health(
    request: HttpRequest,
    context: InvocationContext
): Promise<HttpResponseInit> {
    context.log(`Health check request received: ${request.method} ${request.url}`);

    return {
        jsonBody: {
            status: "ok",
            timestamp: new Date().toISOString()
        }
    };
}

app.http("health", {
    methods: ["GET"],
    authLevel: "anonymous",
    handler: health
});