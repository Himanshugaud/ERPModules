using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;

namespace ERP.Api.Middleware;

/// Populates IHttpContextAccessor from the Functions ASP.NET Core integration,
/// which is not wired automatically in the isolated worker.
public sealed class HttpContextMiddleware : IFunctionsWorkerMiddleware
{
    private readonly IHttpContextAccessor _accessor;
    public HttpContextMiddleware(IHttpContextAccessor accessor) => _accessor = accessor;

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var http = context.GetHttpContext();
        if (http is not null)
            _accessor.HttpContext = http;
        await next(context);
    }
}
