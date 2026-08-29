using System.Text.Json;
using ERP.Shared.Exceptions;
using ERP.Shared.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;

namespace ERP.Api.Middleware;

public sealed class ExceptionHandlingMiddleware : IFunctionsWorkerMiddleware
{
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public ExceptionHandlingMiddleware(ILogger<ExceptionHandlingMiddleware> logger) => _logger = logger;

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            var http = context.GetHttpContext();
            if (http is null) throw;

            var (status, code, message, details) = Map(ex);
            if (status >= 500)
                _logger.LogError(ex, "Unhandled exception");
            else
                _logger.LogWarning("Handled {Code}: {Message}", code, ex.Message);

            var body = new ApiErrorResponse
            {
                Error = new ApiError
                {
                    Code = code,
                    Message = message,
                    TraceId = http.TraceIdentifier,
                    Details = details
                }
            };

            http.Response.StatusCode = status;
            http.Response.ContentType = "application/json";
            await http.Response.WriteAsync(JsonSerializer.Serialize(body, JsonOptions));
        }
    }

    private static (int status, string code, string message, IReadOnlyDictionary<string, string[]>? details) Map(Exception ex) => ex switch
    {
        ValidationException v => (400, v.Code, v.Message, v.Errors),
        UnauthorizedException u => (401, u.Code, u.Message, null),
        ForbiddenException f => (403, f.Code, f.Message, null),
        NotFoundException n => (404, n.Code, n.Message, null),
        ConcurrencyException c => (409, c.Code, c.Message, null),
        DuplicateEntityException d => (409, d.Code, d.Message, null),
        ConflictException c => (409, c.Code, c.Message, null),
        AppException a => (400, a.Code, a.Message, null),
        _ => (500, "INTERNAL_ERROR", "An unexpected error occurred.", null)
    };
}
