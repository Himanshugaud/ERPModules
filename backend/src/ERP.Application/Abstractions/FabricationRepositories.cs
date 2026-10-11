namespace ERP.Application.Abstractions;

/// <summary>Reads used to decide which requirement lines must be fabricated in the factory.</summary>
public interface IFabricationRepository
{
    Task<HashSet<Guid>> ManufacturedItemIdsAsync(Guid organizationId, IReadOnlyCollection<Guid> itemIds, CancellationToken ct = default);

    /// <summary>Available stock (on hand minus reserved) per item across all warehouses.</summary>
    Task<Dictionary<Guid, decimal>> AvailableStockAsync(Guid organizationId, IReadOnlyCollection<Guid> itemIds, CancellationToken ct = default);

    /// <summary>Production store first, then the preferred warehouse, then any active warehouse.</summary>
    Task<Guid?> ProductionWarehouseIdAsync(Guid organizationId, Guid? preferredWarehouseId, CancellationToken ct = default);

    /// <summary>Item ids that already have a fabrication work order for the requirement.</summary>
    Task<HashSet<Guid>> FabricatedItemsForRequirementAsync(Guid organizationId, Guid requirementId, CancellationToken ct = default);
}
