namespace ERP.Domain.Constants;

public static class MaterialRequirementStatuses
{
    public const string Submitted = "SUBMITTED";
    public const string Approved = "APPROVED";
    public const string Rejected = "REJECTED";
    public const string Converted = "CONVERTED";
    public const string Cancelled = "CANCELLED";
}

public static class PurchaseOrderStatuses
{
    public const string Draft = "DRAFT";
    public const string PendingApproval = "PENDING_APPROVAL";
    public const string Approved = "APPROVED";
    public const string Ordered = "ORDERED";
    public const string PartiallyReceived = "PARTIALLY_RECEIVED";
    public const string Received = "RECEIVED";
    public const string Closed = "CLOSED";
    public const string Rejected = "REJECTED";
    public const string Cancelled = "CANCELLED";
}

public static class Priorities
{
    public const string Low = "LOW";
    public const string Medium = "MEDIUM";
    public const string High = "HIGH";
    public const string Urgent = "URGENT";

    public static readonly string[] All = { Low, Medium, High, Urgent };
}
