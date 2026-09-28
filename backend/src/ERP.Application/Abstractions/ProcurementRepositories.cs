using ERP.Domain.Entities;
using ERP.Shared.Pagination;

namespace ERP.Application.Abstractions;

public sealed class MaterialRequirementFilter : PageRequest
{
    public Guid? ProjectId { get; set; }
    public Guid? WarehouseId { get; set; }
    public string? Status { get; set; }
    public string? Priority { get; set; }
}

public sealed class PurchaseOrderFilter : PageRequest
{
    public Guid? SupplierId { get; set; }
    public Guid? ProjectId { get; set; }
    public string? Status { get; set; }
}

public interface IMaterialRequirementRepository
{
    Task<MaterialRequirement?> GetAsync(Guid organizationId, Guid id, bool track, CancellationToken ct = default);
    Task<PagedResult<MaterialRequirement>> ListAsync(Guid organizationId, MaterialRequirementFilter filter, CancellationToken ct = default);
    Task<bool> NumberExistsAsync(Guid organizationId, string number, CancellationToken ct = default);
    Task AddAsync(MaterialRequirement requirement, CancellationToken ct = default);
}

public interface IPurchaseOrderRepository
{
    Task<PurchaseOrder?> GetAsync(Guid organizationId, Guid id, bool track, CancellationToken ct = default);
    Task<PagedResult<PurchaseOrder>> ListAsync(Guid organizationId, PurchaseOrderFilter filter, CancellationToken ct = default);
    Task<bool> NumberExistsAsync(Guid organizationId, string number, CancellationToken ct = default);
    Task AddAsync(PurchaseOrder order, CancellationToken ct = default);
}
