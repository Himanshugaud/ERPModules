namespace ERP.Domain.Constants;

public static class DocumentCategories
{
    public const string Requirement = "REQUIREMENT";
    public const string Planning = "PLANNING";
    public const string Procurement = "PROCUREMENT";
    public const string Transport = "TRANSPORT";
    public const string Other = "OTHER";

    // Category -> blob folder name (shown to users in the storage account).
    public static readonly IReadOnlyDictionary<string, string> Folders = new Dictionary<string, string>
    {
        [Requirement] = "Requirement Attachments",
        [Planning] = "Planning Attachments",
        [Procurement] = "Procurement Documents",
        [Transport] = "Transport Documents",
        [Other] = "Other Attachments"
    };

    public static bool IsValid(string? category) => category is not null && Folders.ContainsKey(category);
}

public static class PlanEntryTypes
{
    public static readonly string[] All = { "ACTIVITY", "MILESTONE", "MATERIAL", "INSPECTION", "MEETING", "OTHER" };
}

public static class PlanEntryStatuses
{
    public static readonly string[] All = { "PLANNED", "IN_PROGRESS", "DONE", "CANCELLED" };
}

public static class PlanStatuses
{
    public const string Draft = "DRAFT";
    public const string Published = "PUBLISHED";
}
