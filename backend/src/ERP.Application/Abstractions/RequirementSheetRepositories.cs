namespace ERP.Application.Abstractions;

public sealed class SheetCatalogItem
{
    public Guid Id { get; set; }
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public Guid? UomId { get; set; }
    public string? UomCode { get; set; }
    public bool IsFabricated { get; set; }
}

public sealed class SheetCatalogSection
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public List<SheetCatalogItem> Items { get; set; } = new();
}

public sealed class SheetCatalogType
{
    public Guid Id { get; set; }
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public List<SheetCatalogSection> Sections { get; set; } = new();
}

public interface IRequirementSheetRepository
{
    Task<List<SheetCatalogType>> GetCatalogAsync(Guid organizationId, CancellationToken ct = default);
}
