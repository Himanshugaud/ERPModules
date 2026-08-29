using ERP.Api.Middleware;
using ERP.Api.Security;
using ERP.Application;
using ERP.Application.Abstractions;
using ERP.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication(worker =>
    {
        worker.UseMiddleware<ExceptionHandlingMiddleware>();
        worker.UseMiddleware<HttpContextMiddleware>();
        worker.UseMiddleware<JwtAuthenticationMiddleware>();
    })
    .ConfigureServices((context, services) =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddScoped<IAuthorizationGuard, AuthorizationGuard>();

        services.AddApplication();
        services.AddInfrastructure(context.Configuration);
    })
    .Build();

host.Run();
