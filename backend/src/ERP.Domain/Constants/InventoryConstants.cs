namespace ERP.Domain.Constants;

public static class ItemTypes
{
    public const string RawMaterial = "RAW_MATERIAL";
    public const string FinishedGood = "FINISHED_GOOD";
    public const string SemiFinished = "SEMI_FINISHED";
    public const string Consumable = "CONSUMABLE";
    public const string SparePart = "SPARE_PART";
    public const string ToolEquipment = "TOOL_EQUIPMENT";

    public static readonly string[] All =
        { RawMaterial, FinishedGood, SemiFinished, Consumable, SparePart, ToolEquipment };
}

public static class ValuationMethods
{
    public const string WeightedAverage = "WEIGHTED_AVG";
    public const string Fifo = "FIFO";
    public const string Standard = "STANDARD";

    public static readonly string[] All = { WeightedAverage, Fifo, Standard };
}

public static class WarehouseTypes
{
    public const string MainStore = "MAIN_STORE";
    public const string SiteStore = "SITE_STORE";
    public const string ProductionStore = "PRODUCTION_STORE";
    public const string Transit = "TRANSIT";
    public const string Scrap = "SCRAP";

    public static readonly string[] All = { MainStore, SiteStore, ProductionStore, Transit, Scrap };
}

public static class MovementTypes
{
    public const string Receipt = "RECEIPT";
    public const string Issue = "ISSUE";
    public const string TransferIn = "TRANSFER_IN";
    public const string TransferOut = "TRANSFER_OUT";
    public const string Adjustment = "ADJUSTMENT";
    public const string ProductionIn = "PRODUCTION_IN";
    public const string Consumption = "CONSUMPTION";
    public const string ReturnIn = "RETURN_IN";
    public const string ReturnOut = "RETURN_OUT";
    public const string Scrap = "SCRAP";
    public const string OpeningBalance = "OPENING_BALANCE";
}

public static class MovementDirection
{
    public const string In = "IN";
    public const string Out = "OUT";
}

public static class RefDocTypes
{
    public const string GoodsReceipt = "GRN";
    public const string MaterialIssue = "ISSUE";
    public const string StockTransfer = "TRANSFER";
    public const string StockAdjustment = "ADJUSTMENT";
}
