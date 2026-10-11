using ERP.Application.Abstractions;
using ERP.Domain.Constants;
using ERP.Domain.Entities;

namespace ERP.Application.Inventory;

public interface IFabricationPlanner
{
    /// <summary>
    /// Raises a DRAFT work order (the fabrication list) for every factory-made item in the requirements that is not
    /// covered by available stock. Must run inside the caller's transaction; the caller saves.
    /// </summary>
    Task<int> RaiseAsync(IEnumerable<MaterialRequirement> requirements, CancellationToken ct = default);
}

public sealed class FabricationPlanner : IFabricationPlanner
{
    private readonly IFabricationRepository _fabrication;
    private readonly IWorkOrderRepository _workOrders;
    private readonly ITenantContext _tenant;
    private readonly IAuditWriter _audit;
    private readonly IClock _clock;

    public FabricationPlanner(IFabricationRepository fabrication, IWorkOrderRepository workOrders, ITenantContext tenant,
        IAuditWriter audit, IClock clock)
    {
        _fabrication = fabrication;
        _workOrders = workOrders;
        _tenant = tenant;
        _audit = audit;
        _clock = clock;
    }

    public async Task<int> RaiseAsync(IEnumerable<MaterialRequirement> requirements, CancellationToken ct = default)
    {
        var orgId = _tenant.OrganizationId;
        var reqs = requirements.Where(r => r.Lines.Count > 0).ToList();
        if (reqs.Count == 0) return 0;

        var itemIds = reqs.SelectMany(r => r.Lines.Select(l => l.ItemId)).Distinct().ToList();
        var fabricated = await _fabrication.ManufacturedItemIdsAsync(orgId, itemIds, ct);
        if (fabricated.Count == 0) return 0;

        // Stock is shared across requirements published together, so each one consumes what the previous left.
        var available = await _fabrication.AvailableStockAsync(orgId, fabricated.ToList(), ct);
        var raised = 0;

        foreach (var req in reqs)
        {
            var alreadyPlanned = await _fabrication.FabricatedItemsForRequirementAsync(orgId, req.Id, ct);
            var warehouseId = await _fabrication.ProductionWarehouseIdAsync(orgId, req.WarehouseId, ct);
            if (warehouseId is null) continue;

            foreach (var group in req.Lines.Where(l => fabricated.Contains(l.ItemId)).GroupBy(l => l.ItemId))
            {
                if (alreadyPlanned.Contains(group.Key)) continue;

                var required = group.Sum(l => l.Qty);
                var onHand = Math.Max(0, available.GetValueOrDefault(group.Key));
                var covered = Math.Min(required, onHand);
                available[group.Key] = onHand - covered;
                var shortage = required - covered;
                if (shortage <= 0) continue;

                var wo = new WorkOrder
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = orgId,
                    WoNumber = InventoryNumbers.Next("WO"),
                    OutputItemId = group.Key,
                    PlannedQty = shortage,
                    WarehouseId = warehouseId.Value,
                    ProjectId = req.ProjectId,
                    MaterialRequirementId = req.Id,
                    Status = WorkOrderStatuses.Draft,
                    StartDate = req.RequiredDate,
                    CreatedAt = _clock.UtcNow,
                    CreatedBy = _tenant.UserId
                };
                await _workOrders.AddAsync(wo, ct);
                _audit.Add(EntityTypes.WorkOrder, wo.Id, AuditActions.Create, null,
                    new { wo.WoNumber, wo.PlannedQty, Source = "PLAN_PUBLISH", req.ReqNumber });
                raised++;
            }
        }
        return raised;
    }
}
