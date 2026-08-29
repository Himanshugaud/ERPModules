using ERP.Application.Abstractions;
using ERP.Domain.Entities;
using ERP.Shared.Exceptions;

namespace ERP.Application.Clients;

public sealed class ClientService : IClientService
{
    private readonly IClientRepository _clients;
    private readonly ITenantContext _tenant;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;

    public ClientService(IClientRepository clients, ITenantContext tenant, IUnitOfWork uow, IClock clock)
    {
        _clients = clients;
        _tenant = tenant;
        _uow = uow;
        _clock = clock;
    }

    public async Task<IReadOnlyList<ClientResponse>> ListAsync(CancellationToken ct = default)
    {
        var items = await _clients.ListAsync(_tenant.OrganizationId, ct);
        return items.Select(Map).ToList();
    }

    public async Task<ClientResponse> GetAsync(Guid clientId, CancellationToken ct = default)
    {
        var client = await _clients.GetAsync(_tenant.OrganizationId, clientId, false, ct)
            ?? throw NotFoundException.For("Client", clientId);
        return Map(client);
    }

    public async Task<ClientResponse> CreateAsync(CreateClientRequest request, CancellationToken ct = default)
    {
        var orgId = _tenant.OrganizationId;
        if (await _clients.CodeExistsAsync(orgId, request.Code, null, ct))
            throw new DuplicateEntityException($"A client with code '{request.Code}' already exists.");

        var client = new Client
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            Name = request.Name,
            Code = request.Code,
            Email = request.Email,
            Phone = request.Phone,
            Status = string.IsNullOrWhiteSpace(request.Status) ? "ACTIVE" : request.Status,
            CreatedAt = _clock.UtcNow
        };

        await _clients.AddAsync(client, ct);
        await _uow.SaveChangesAsync(ct);
        return Map(client);
    }

    public async Task<ClientResponse> UpdateAsync(Guid clientId, UpdateClientRequest request, CancellationToken ct = default)
    {
        var client = await _clients.GetAsync(_tenant.OrganizationId, clientId, true, ct)
            ?? throw NotFoundException.For("Client", clientId);

        client.Name = request.Name;
        client.Email = request.Email;
        client.Phone = request.Phone;
        if (!string.IsNullOrWhiteSpace(request.Status)) client.Status = request.Status;
        client.UpdatedAt = _clock.UtcNow;

        await _uow.SaveChangesAsync(ct);
        return Map(client);
    }

    private static ClientResponse Map(Client c) => new()
    {
        Id = c.Id,
        Name = c.Name,
        Code = c.Code,
        Email = c.Email,
        Phone = c.Phone,
        Status = c.Status,
        CreatedAt = c.CreatedAt,
        UpdatedAt = c.UpdatedAt
    };
}
