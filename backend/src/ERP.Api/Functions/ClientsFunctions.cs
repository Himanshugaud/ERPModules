using ERP.Api.Common;
using ERP.Api.Security;
using ERP.Application.Clients;
using ERP.Domain.Constants;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace ERP.Api.Functions;

public sealed class ClientsFunctions
{
    private readonly IClientService _service;
    private readonly IAuthorizationGuard _auth;
    private readonly IValidator<CreateClientRequest> _createValidator;
    private readonly IValidator<UpdateClientRequest> _updateValidator;

    public ClientsFunctions(IClientService service, IAuthorizationGuard auth,
        IValidator<CreateClientRequest> createValidator, IValidator<UpdateClientRequest> updateValidator)
    {
        _service = service;
        _auth = auth;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    [Function("ListClients")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/clients")] HttpRequest req, CancellationToken ct)
    {
        _auth.Require(Permissions.ProjectRead);
        return Http.Ok(await _service.ListAsync(ct));
    }

    [Function("GetClient")]
    public async Task<IActionResult> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/clients/{clientId:guid}")] HttpRequest req,
        Guid clientId, CancellationToken ct)
    {
        _auth.Require(Permissions.ProjectRead);
        return Http.Ok(await _service.GetAsync(clientId, ct));
    }

    [Function("CreateClient")]
    public async Task<IActionResult> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/clients")] HttpRequest req, CancellationToken ct)
    {
        // Prototype: any authenticated org member may manage clients (no client.* permission exists yet).
        _auth.RequireAuthenticated();
        var body = await Http.ReadValidatedAsync(req, _createValidator, ct);
        return Http.Created(await _service.CreateAsync(body, ct));
    }

    [Function("UpdateClient")]
    public async Task<IActionResult> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/clients/{clientId:guid}")] HttpRequest req,
        Guid clientId, CancellationToken ct)
    {
        _auth.RequireAuthenticated();
        var body = await Http.ReadValidatedAsync(req, _updateValidator, ct);
        return Http.Ok(await _service.UpdateAsync(clientId, body, ct));
    }
}
