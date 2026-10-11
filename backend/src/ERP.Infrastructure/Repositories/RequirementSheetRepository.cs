using ERP.Application.Abstractions;
using ERP.Domain.Entities;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Repositories;

public sealed class RequirementSheetRepository : IRequirementSheetRepository
{
    private readonly ErpDbContext _db;
    public RequirementSheetRepository(ErpDbContext db) => _db = db;

    public async Task<List<SheetCatalogType>> GetCatalogAsync(Guid organizationId, CancellationToken ct = default)
    {
        var types = await _db.RequirementSheetTypes.AsNoTracking()
            .Where(t => t.OrganizationId == organizationId && t.IsActive).OrderBy(t => t.DisplayOrder).ToListAsync(ct);
        if (types.Count == 0) return new List<SheetCatalogType>();

        var typeIds = types.Select(t => t.Id).ToList();
        var sections = await _db.RequirementSheetSections.AsNoTracking()
            .Where(s => typeIds.Contains(s.SheetTypeId) && s.IsActive).OrderBy(s => s.DisplayOrder).ToListAsync(ct);
        var sectionIds = sections.Select(s => s.Id).ToList();
        var mappings = await _db.RequirementSheetSectionItems.AsNoTracking()
            .Where(m => sectionIds.Contains(m.SectionId)).OrderBy(m => m.DisplayOrder).ToListAsync(ct);

        // The universal item list: every active item, so sheets always reflect what Inventory maintains.
        var items = await _db.Items.AsNoTracking()
            .Where(i => i.OrganizationId == organizationId && !i.IsDeleted && i.IsActive)
            .OrderBy(i => i.Name).ToListAsync(ct);
        var categories = await _db.ItemCategories.AsNoTracking()
            .Where(c => c.OrganizationId == organizationId && c.IsActive).ToListAsync(ct);
        var uoms = (await _db.UnitsOfMeasure.AsNoTracking().Where(u => u.OrganizationId == organizationId).ToListAsync(ct))
            .ToDictionary(u => u.Id);

        SheetCatalogItem Map(Item i) => new()
        {
            Id = i.Id,
            Code = i.Code,
            Name = i.Name,
            UomId = i.BaseUomId,
            UomCode = i.BaseUomId.HasValue && uoms.TryGetValue(i.BaseUomId.Value, out var u) ? u.Code : null,
            IsFabricated = i.IsManufactured
        };

        // Category id plus all descendants.
        HashSet<Guid> Subtree(Guid rootId)
        {
            var set = new HashSet<Guid> { rootId };
            var queue = new Queue<Guid>();
            queue.Enqueue(rootId);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var child in categories.Where(c => c.ParentCategoryId == current))
                    if (set.Add(child.Id)) queue.Enqueue(child.Id);
            }
            return set;
        }

        var result = new List<SheetCatalogType>();
        foreach (var type in types)
        {
            var catalogType = new SheetCatalogType { Id = type.Id, Code = type.Code, Name = type.Name, Description = type.Description };

            if (type.CategoryId.HasValue)
            {
                // Sections are the sub-categories of the sheet's category; items come straight from the item master.
                var root = type.CategoryId.Value;
                foreach (var sub in categories.Where(c => c.ParentCategoryId == root).OrderBy(c => c.SortOrder).ThenBy(c => c.Name))
                {
                    var ids = Subtree(sub.Id);
                    var subItems = items.Where(i => i.CategoryId.HasValue && ids.Contains(i.CategoryId.Value)).Select(Map).ToList();
                    catalogType.Sections.Add(new SheetCatalogSection { Id = sub.Id, Name = sub.Name, Items = subItems });
                }
                var direct = items.Where(i => i.CategoryId == root).Select(Map).ToList();
                if (direct.Count > 0) catalogType.Sections.Add(new SheetCatalogSection { Id = root, Name = "Other", Items = direct });
                result.Add(catalogType);
                continue;
            }

            var typeSections = sections.Where(s => s.SheetTypeId == type.Id).ToList();
            // An item explicitly mapped to a section is removed from category-fed sections of the same sheet type.
            var explicitIds = mappings.Where(m => m.ItemId.HasValue && typeSections.Any(s => s.Id == m.SectionId))
                .Select(m => m.ItemId!.Value).ToHashSet();
            var placed = new HashSet<Guid>();

            foreach (var section in typeSections)
            {
                var sectionMaps = mappings.Where(m => m.SectionId == section.Id).ToList();
                var explicitHere = sectionMaps.Where(m => m.ItemId.HasValue).Select(m => m.ItemId!.Value).ToHashSet();
                var cats = sectionMaps.Where(m => m.CategoryId.HasValue).Select(m => m.CategoryId!.Value).ToHashSet();

                var sectionItems = items
                    .Where(i => explicitHere.Contains(i.Id) || (i.CategoryId.HasValue && cats.Contains(i.CategoryId.Value) && !explicitIds.Contains(i.Id)))
                    .Where(i => placed.Add(i.Id))
                    .Select(Map).ToList();

                catalogType.Sections.Add(new SheetCatalogSection { Id = section.Id, Name = section.Name, Items = sectionItems });
            }
            result.Add(catalogType);
        }
        return result;
    }
}
