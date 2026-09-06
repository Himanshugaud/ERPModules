namespace ERP.Domain.Constants;

public static class Permissions
{
    public const string ProjectRead = "project.read";
    public const string ProjectCreate = "project.create";
    public const string ProjectUpdate = "project.update";
    public const string ProjectDelete = "project.delete";

    public const string TaskRead = "task.read";
    public const string TaskCreate = "task.create";
    public const string TaskUpdate = "task.update";
    public const string TaskDelete = "task.delete";
    public const string TaskAssign = "task.assign";

    public const string TimesheetRead = "timesheet.read";
    public const string TimesheetCreate = "timesheet.create";
    public const string TimesheetUpdate = "timesheet.update";
    public const string TimesheetApprove = "timesheet.approve";
    public const string TimesheetReject = "timesheet.reject";

    public const string DocumentRead = "document.read";
    public const string DocumentCreate = "document.create";
    public const string DocumentDelete = "document.delete";

    public const string AuditRead = "audit.read";
    public const string NotificationRead = "notification.read";

    // Inventory
    public const string ItemRead = "item.read";
    public const string ItemCreate = "item.create";
    public const string ItemUpdate = "item.update";
    public const string ItemDelete = "item.delete";

    public const string WarehouseRead = "warehouse.read";
    public const string WarehouseCreate = "warehouse.create";
    public const string WarehouseUpdate = "warehouse.update";
    public const string WarehouseDelete = "warehouse.delete";

    public const string SupplierRead = "supplier.read";
    public const string SupplierCreate = "supplier.create";
    public const string SupplierUpdate = "supplier.update";
    public const string SupplierDelete = "supplier.delete";

    public const string StockRead = "stock.read";
    public const string StockAdjust = "stock.adjust";

    public const string GoodsReceiptRead = "goodsreceipt.read";
    public const string GoodsReceiptCreate = "goodsreceipt.create";

    public const string IssueRead = "issue.read";
    public const string IssueCreate = "issue.create";

    public const string TransferRead = "transfer.read";
    public const string TransferCreate = "transfer.create";

    public const string InventoryReportRead = "inventory.report.read";
}

public static class EntityTypes
{
    public const string Project = "PROJECT";
    public const string Task = "TASK";
    public const string Milestone = "MILESTONE";
    public const string Sprint = "SPRINT";
    public const string Timesheet = "TIMESHEET";
    public const string Document = "DOCUMENT";
    public const string Client = "CLIENT";
    public const string Item = "ITEM";
    public const string Warehouse = "WAREHOUSE";
    public const string Supplier = "SUPPLIER";
    public const string GoodsReceipt = "GOODS_RECEIPT";
    public const string MaterialIssue = "MATERIAL_ISSUE";
    public const string StockTransfer = "STOCK_TRANSFER";
    public const string StockAdjustment = "STOCK_ADJUSTMENT";
}

public static class AuditActions
{
    public const string Create = "CREATE";
    public const string Update = "UPDATE";
    public const string Delete = "DELETE";
    public const string StatusChange = "STATUS_CHANGE";
    public const string Assign = "ASSIGN";
    public const string Approve = "APPROVE";
    public const string Reject = "REJECT";
}

public static class SystemRoles
{
    public const string SuperAdmin = "SUPER_ADMIN";
    public const string Admin = "ADMIN";
    public const string ProjectManager = "PROJECT_MANAGER";
    public const string ProjectMember = "PROJECT_MEMBER";
    public const string Viewer = "VIEWER";

    public static readonly string[] Administrative = { SuperAdmin, Admin };
}
