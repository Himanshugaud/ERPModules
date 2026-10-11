namespace ERP.Domain.Entities;

public class RequirementSheetType
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    /// <summary>Root item category feeding this sheet; its sub-categories become the sheet sections.</summary>
    public Guid? CategoryId { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}

public class RequirementSheetSection
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid SheetTypeId { get; set; }
    public string Name { get; set; } = default!;
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Either a whole item category or one explicit item feeds a section.</summary>
public class RequirementSheetSectionItem
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid SectionId { get; set; }
    public Guid? CategoryId { get; set; }
    public Guid? ItemId { get; set; }
    public int DisplayOrder { get; set; }
}
