/* =============================================================================
   ERP — INVENTORY MODULE SCHEMA
   Idempotent, GO-batched. Additive only: creates a new `inventory` schema.
   Does NOT modify existing core / project / shared tables.
   Apply the same way as erp-schema.sql (node+mssql, split on /^\s*GO\s*$/).
   ========================================================================== */

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'inventory')
    EXEC(N'CREATE SCHEMA inventory');
GO

/* -----------------------------------------------------------------------------
   MASTER DATA
   -------------------------------------------------------------------------- */

IF OBJECT_ID(N'inventory.ItemCategories', N'U') IS NULL
BEGIN
    CREATE TABLE inventory.ItemCategories
    (
        Id               UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ItemCategories PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        OrganizationId   UNIQUEIDENTIFIER NOT NULL,
        Code             NVARCHAR(50)     NOT NULL,
        Name             NVARCHAR(150)    NOT NULL,
        Description      NVARCHAR(500)    NULL,
        ParentCategoryId UNIQUEIDENTIFIER NULL,
        IsActive         BIT              NOT NULL CONSTRAINT DF_ItemCategories_IsActive DEFAULT 1,
        CreatedAt        DATETIME2        NOT NULL CONSTRAINT DF_ItemCategories_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt        DATETIME2        NULL,
        CreatedBy        UNIQUEIDENTIFIER NULL,
        UpdatedBy        UNIQUEIDENTIFIER NULL,
        CONSTRAINT UQ_ItemCategories_Org_Code UNIQUE (OrganizationId, Code)
    );
END
GO

IF OBJECT_ID(N'inventory.UnitsOfMeasure', N'U') IS NULL
BEGIN
    CREATE TABLE inventory.UnitsOfMeasure
    (
        Id             UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_UnitsOfMeasure PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        OrganizationId UNIQUEIDENTIFIER NOT NULL,
        Code           NVARCHAR(20)     NOT NULL,
        Name           NVARCHAR(100)    NOT NULL,
        IsBaseUnit     BIT              NOT NULL CONSTRAINT DF_Uom_IsBaseUnit DEFAULT 0,
        IsActive       BIT              NOT NULL CONSTRAINT DF_Uom_IsActive DEFAULT 1,
        CreatedAt      DATETIME2        NOT NULL CONSTRAINT DF_Uom_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt      DATETIME2        NULL,
        CONSTRAINT UQ_Uom_Org_Code UNIQUE (OrganizationId, Code)
    );
END
GO

IF OBJECT_ID(N'inventory.UomConversions', N'U') IS NULL
BEGIN
    CREATE TABLE inventory.UomConversions
    (
        Id             UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_UomConversions PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        OrganizationId UNIQUEIDENTIFIER NOT NULL,
        FromUomId      UNIQUEIDENTIFIER NOT NULL,
        ToUomId        UNIQUEIDENTIFIER NOT NULL,
        Factor         DECIMAL(19,6)    NOT NULL,
        CreatedAt      DATETIME2        NOT NULL CONSTRAINT DF_UomConversions_CreatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_UomConversions UNIQUE (OrganizationId, FromUomId, ToUomId),
        CONSTRAINT CK_UomConversions_Factor CHECK (Factor > 0)
    );
END
GO

IF OBJECT_ID(N'inventory.Items', N'U') IS NULL
BEGIN
    CREATE TABLE inventory.Items
    (
        Id              UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Items PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        OrganizationId  UNIQUEIDENTIFIER NOT NULL,
        Code            NVARCHAR(50)     NOT NULL,
        Name            NVARCHAR(200)    NOT NULL,
        Description     NVARCHAR(1000)   NULL,
        ItemType        NVARCHAR(30)     NOT NULL,
        CategoryId      UNIQUEIDENTIFIER NULL,
        BaseUomId       UNIQUEIDENTIFIER NULL,
        Barcode         NVARCHAR(100)    NULL,
        TrackBatches    BIT              NOT NULL CONSTRAINT DF_Items_TrackBatches DEFAULT 0,
        TrackSerials    BIT              NOT NULL CONSTRAINT DF_Items_TrackSerials DEFAULT 0,
        TrackExpiry     BIT              NOT NULL CONSTRAINT DF_Items_TrackExpiry DEFAULT 0,
        ValuationMethod NVARCHAR(20)     NOT NULL CONSTRAINT DF_Items_Valuation DEFAULT N'WEIGHTED_AVG',
        StandardCost    DECIMAL(19,4)    NULL,
        ReorderLevel    DECIMAL(19,4)    NULL,
        SafetyStock     DECIMAL(19,4)    NULL,
        MinStock        DECIMAL(19,4)    NULL,
        MaxStock        DECIMAL(19,4)    NULL,
        ReorderQty      DECIMAL(19,4)    NULL,
        IsPurchasable   BIT              NOT NULL CONSTRAINT DF_Items_IsPurchasable DEFAULT 1,
        IsManufactured  BIT              NOT NULL CONSTRAINT DF_Items_IsManufactured DEFAULT 0,
        IsSellable      BIT              NOT NULL CONSTRAINT DF_Items_IsSellable DEFAULT 0,
        IsActive        BIT              NOT NULL CONSTRAINT DF_Items_IsActive DEFAULT 1,
        IsDeleted       BIT              NOT NULL CONSTRAINT DF_Items_IsDeleted DEFAULT 0,
        DeletedAt       DATETIME2        NULL,
        DeletedBy       UNIQUEIDENTIFIER NULL,
        CreatedAt       DATETIME2        NOT NULL CONSTRAINT DF_Items_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt       DATETIME2        NULL,
        CreatedBy       UNIQUEIDENTIFIER NULL,
        UpdatedBy       UNIQUEIDENTIFIER NULL,
        RowVersion      ROWVERSION       NOT NULL,
        CONSTRAINT UQ_Items_Org_Code UNIQUE (OrganizationId, Code),
        CONSTRAINT CK_Items_ItemType CHECK (ItemType IN
            (N'RAW_MATERIAL', N'FINISHED_GOOD', N'SEMI_FINISHED', N'CONSUMABLE', N'SPARE_PART', N'TOOL_EQUIPMENT')),
        CONSTRAINT CK_Items_Valuation CHECK (ValuationMethod IN (N'WEIGHTED_AVG', N'FIFO', N'STANDARD'))
    );
END
GO

IF OBJECT_ID(N'inventory.Warehouses', N'U') IS NULL
BEGIN
    CREATE TABLE inventory.Warehouses
    (
        Id             UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Warehouses PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        OrganizationId UNIQUEIDENTIFIER NOT NULL,
        Code           NVARCHAR(50)     NOT NULL,
        Name           NVARCHAR(200)    NOT NULL,
        WarehouseType  NVARCHAR(30)     NOT NULL CONSTRAINT DF_Warehouses_Type DEFAULT N'MAIN_STORE',
        ProjectId      UNIQUEIDENTIFIER NULL,
        Address        NVARCHAR(500)    NULL,
        IsActive       BIT              NOT NULL CONSTRAINT DF_Warehouses_IsActive DEFAULT 1,
        CreatedAt      DATETIME2        NOT NULL CONSTRAINT DF_Warehouses_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt      DATETIME2        NULL,
        CreatedBy      UNIQUEIDENTIFIER NULL,
        UpdatedBy      UNIQUEIDENTIFIER NULL,
        CONSTRAINT UQ_Warehouses_Org_Code UNIQUE (OrganizationId, Code),
        CONSTRAINT CK_Warehouses_Type CHECK (WarehouseType IN
            (N'MAIN_STORE', N'SITE_STORE', N'PRODUCTION_STORE', N'TRANSIT', N'SCRAP'))
    );
END
GO

IF OBJECT_ID(N'inventory.StorageBins', N'U') IS NULL
BEGIN
    CREATE TABLE inventory.StorageBins
    (
        Id             UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_StorageBins PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        OrganizationId UNIQUEIDENTIFIER NOT NULL,
        WarehouseId    UNIQUEIDENTIFIER NOT NULL,
        Code           NVARCHAR(50)     NOT NULL,
        Name           NVARCHAR(150)    NULL,
        IsActive       BIT              NOT NULL CONSTRAINT DF_StorageBins_IsActive DEFAULT 1,
        CreatedAt      DATETIME2        NOT NULL CONSTRAINT DF_StorageBins_CreatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_StorageBins_Wh_Code UNIQUE (WarehouseId, Code)
    );
END
GO

IF OBJECT_ID(N'inventory.Suppliers', N'U') IS NULL
BEGIN
    CREATE TABLE inventory.Suppliers
    (
        Id             UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Suppliers PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        OrganizationId UNIQUEIDENTIFIER NOT NULL,
        Code           NVARCHAR(50)     NOT NULL,
        Name           NVARCHAR(200)    NOT NULL,
        Email          NVARCHAR(255)    NULL,
        Phone          NVARCHAR(50)     NULL,
        Address        NVARCHAR(500)    NULL,
        Status         NVARCHAR(30)     NOT NULL CONSTRAINT DF_Suppliers_Status DEFAULT N'ACTIVE',
        CreatedAt      DATETIME2        NOT NULL CONSTRAINT DF_Suppliers_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt      DATETIME2        NULL,
        CreatedBy      UNIQUEIDENTIFIER NULL,
        UpdatedBy      UNIQUEIDENTIFIER NULL,
        CONSTRAINT UQ_Suppliers_Org_Code UNIQUE (OrganizationId, Code)
    );
END
GO

IF OBJECT_ID(N'inventory.ItemSuppliers', N'U') IS NULL
BEGIN
    CREATE TABLE inventory.ItemSuppliers
    (
        Id             UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ItemSuppliers PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        OrganizationId UNIQUEIDENTIFIER NOT NULL,
        ItemId         UNIQUEIDENTIFIER NOT NULL,
        SupplierId     UNIQUEIDENTIFIER NOT NULL,
        SupplierSku    NVARCHAR(100)    NULL,
        LeadTimeDays   INT              NULL,
        LastPrice      DECIMAL(19,4)    NULL,
        IsPreferred    BIT              NOT NULL CONSTRAINT DF_ItemSuppliers_IsPreferred DEFAULT 0,
        CreatedAt      DATETIME2        NOT NULL CONSTRAINT DF_ItemSuppliers_CreatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_ItemSuppliers UNIQUE (ItemId, SupplierId)
    );
END
GO

/* -----------------------------------------------------------------------------
   STOCK & LOTS
   -------------------------------------------------------------------------- */

IF OBJECT_ID(N'inventory.Batches', N'U') IS NULL
BEGIN
    CREATE TABLE inventory.Batches
    (
        Id              UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Batches PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        OrganizationId  UNIQUEIDENTIFIER NOT NULL,
        ItemId          UNIQUEIDENTIFIER NOT NULL,
        BatchNo         NVARCHAR(100)    NOT NULL,
        ManufactureDate DATE             NULL,
        ExpiryDate      DATE             NULL,
        SupplierId      UNIQUEIDENTIFIER NULL,
        QcStatus        NVARCHAR(30)     NOT NULL CONSTRAINT DF_Batches_QcStatus DEFAULT N'RELEASED',
        CreatedAt       DATETIME2        NOT NULL CONSTRAINT DF_Batches_CreatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_Batches_Item_BatchNo UNIQUE (ItemId, BatchNo)
    );
END
GO

IF OBJECT_ID(N'inventory.SerialNumbers', N'U') IS NULL
BEGIN
    CREATE TABLE inventory.SerialNumbers
    (
        Id             UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_SerialNumbers PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        OrganizationId UNIQUEIDENTIFIER NOT NULL,
        ItemId         UNIQUEIDENTIFIER NOT NULL,
        SerialNo       NVARCHAR(100)    NOT NULL,
        WarehouseId    UNIQUEIDENTIFIER NULL,
        Status         NVARCHAR(30)     NOT NULL CONSTRAINT DF_SerialNumbers_Status DEFAULT N'IN_STOCK',
        CreatedAt      DATETIME2        NOT NULL CONSTRAINT DF_SerialNumbers_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt      DATETIME2        NULL,
        CONSTRAINT UQ_SerialNumbers_Item_Serial UNIQUE (ItemId, SerialNo)
    );
END
GO

IF OBJECT_ID(N'inventory.StockLevels', N'U') IS NULL
BEGIN
    CREATE TABLE inventory.StockLevels
    (
        Id             UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_StockLevels PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        OrganizationId UNIQUEIDENTIFIER NOT NULL,
        ItemId         UNIQUEIDENTIFIER NOT NULL,
        WarehouseId    UNIQUEIDENTIFIER NOT NULL,
        BinId          UNIQUEIDENTIFIER NULL,
        BatchId        UNIQUEIDENTIFIER NULL,
        QtyOnHand      DECIMAL(19,4)    NOT NULL CONSTRAINT DF_StockLevels_OnHand DEFAULT 0,
        QtyReserved    DECIMAL(19,4)    NOT NULL CONSTRAINT DF_StockLevels_Reserved DEFAULT 0,
        QtyInTransit   DECIMAL(19,4)    NOT NULL CONSTRAINT DF_StockLevels_InTransit DEFAULT 0,
        AvgUnitCost    DECIMAL(19,4)    NOT NULL CONSTRAINT DF_StockLevels_AvgCost DEFAULT 0,
        UpdatedAt      DATETIME2        NULL,
        RowVersion     ROWVERSION       NOT NULL,
        CONSTRAINT UQ_StockLevels_Item_Wh_Batch UNIQUE (ItemId, WarehouseId, BatchId)
    );
END
GO

IF OBJECT_ID(N'inventory.StockMovements', N'U') IS NULL
BEGIN
    CREATE TABLE inventory.StockMovements
    (
        Id             UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_StockMovements PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        OrganizationId UNIQUEIDENTIFIER NOT NULL,
        ItemId         UNIQUEIDENTIFIER NOT NULL,
        WarehouseId    UNIQUEIDENTIFIER NOT NULL,
        BinId          UNIQUEIDENTIFIER NULL,
        BatchId        UNIQUEIDENTIFIER NULL,
        SerialId       UNIQUEIDENTIFIER NULL,
        MovementType   NVARCHAR(30)     NOT NULL,
        Direction      NVARCHAR(3)      NOT NULL,
        Qty            DECIMAL(19,4)    NOT NULL,
        UnitCost       DECIMAL(19,4)    NOT NULL CONSTRAINT DF_StockMovements_UnitCost DEFAULT 0,
        TotalCost      DECIMAL(19,4)    NOT NULL CONSTRAINT DF_StockMovements_TotalCost DEFAULT 0,
        RefDocType     NVARCHAR(30)     NULL,
        RefDocId       UNIQUEIDENTIFIER NULL,
        RefDocLineId   UNIQUEIDENTIFIER NULL,
        ProjectId      UNIQUEIDENTIFIER NULL,
        OccurredAt     DATETIME2        NOT NULL CONSTRAINT DF_StockMovements_OccurredAt DEFAULT SYSUTCDATETIME(),
        CreatedBy      UNIQUEIDENTIFIER NULL,
        CONSTRAINT CK_StockMovements_Direction CHECK (Direction IN (N'IN', N'OUT')),
        CONSTRAINT CK_StockMovements_Qty CHECK (Qty > 0)
    );
END
GO

/* -----------------------------------------------------------------------------
   TRANSACTIONS — DOCUMENTS
   -------------------------------------------------------------------------- */

IF OBJECT_ID(N'inventory.PurchaseOrders', N'U') IS NULL
BEGIN
    CREATE TABLE inventory.PurchaseOrders
    (
        Id             UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PurchaseOrders PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        OrganizationId UNIQUEIDENTIFIER NOT NULL,
        PoNumber       NVARCHAR(50)     NOT NULL,
        SupplierId     UNIQUEIDENTIFIER NOT NULL,
        Status         NVARCHAR(30)     NOT NULL CONSTRAINT DF_PurchaseOrders_Status DEFAULT N'DRAFT',
        OrderDate      DATE             NULL,
        ExpectedDate   DATE             NULL,
        CurrencyCode   CHAR(3)          NULL,
        TotalAmount    DECIMAL(19,4)    NOT NULL CONSTRAINT DF_PurchaseOrders_Total DEFAULT 0,
        Notes          NVARCHAR(1000)   NULL,
        CreatedAt      DATETIME2        NOT NULL CONSTRAINT DF_PurchaseOrders_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt      DATETIME2        NULL,
        CreatedBy      UNIQUEIDENTIFIER NULL,
        UpdatedBy      UNIQUEIDENTIFIER NULL,
        RowVersion     ROWVERSION       NOT NULL,
        CONSTRAINT UQ_PurchaseOrders_Org_No UNIQUE (OrganizationId, PoNumber)
    );
END
GO

IF OBJECT_ID(N'inventory.PurchaseOrderLines', N'U') IS NULL
BEGIN
    CREATE TABLE inventory.PurchaseOrderLines
    (
        Id              UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PurchaseOrderLines PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        PurchaseOrderId UNIQUEIDENTIFIER NOT NULL,
        ItemId          UNIQUEIDENTIFIER NOT NULL,
        Qty             DECIMAL(19,4)    NOT NULL,
        UomId           UNIQUEIDENTIFIER NULL,
        UnitPrice       DECIMAL(19,4)    NOT NULL CONSTRAINT DF_PurchaseOrderLines_Price DEFAULT 0,
        LineTotal       DECIMAL(19,4)    NOT NULL CONSTRAINT DF_PurchaseOrderLines_Total DEFAULT 0,
        QtyReceived     DECIMAL(19,4)    NOT NULL CONSTRAINT DF_PurchaseOrderLines_Received DEFAULT 0
    );
END
GO

IF OBJECT_ID(N'inventory.GoodsReceipts', N'U') IS NULL
BEGIN
    CREATE TABLE inventory.GoodsReceipts
    (
        Id              UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_GoodsReceipts PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        OrganizationId  UNIQUEIDENTIFIER NOT NULL,
        GrnNumber       NVARCHAR(50)     NOT NULL,
        SupplierId      UNIQUEIDENTIFIER NULL,
        PurchaseOrderId UNIQUEIDENTIFIER NULL,
        PoReference     NVARCHAR(50)     NULL,
        WarehouseId     UNIQUEIDENTIFIER NOT NULL,
        ReceivedDate    DATE             NULL,
        Status          NVARCHAR(30)     NOT NULL CONSTRAINT DF_GoodsReceipts_Status DEFAULT N'POSTED',
        Notes           NVARCHAR(1000)   NULL,
        CreatedAt       DATETIME2        NOT NULL CONSTRAINT DF_GoodsReceipts_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt       DATETIME2        NULL,
        CreatedBy       UNIQUEIDENTIFIER NULL,
        UpdatedBy       UNIQUEIDENTIFIER NULL,
        CONSTRAINT UQ_GoodsReceipts_Org_No UNIQUE (OrganizationId, GrnNumber)
    );
END
GO

IF OBJECT_ID(N'inventory.GoodsReceiptLines', N'U') IS NULL
BEGIN
    CREATE TABLE inventory.GoodsReceiptLines
    (
        Id             UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_GoodsReceiptLines PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        GoodsReceiptId UNIQUEIDENTIFIER NOT NULL,
        ItemId         UNIQUEIDENTIFIER NOT NULL,
        Qty            DECIMAL(19,4)    NOT NULL,
        UomId          UNIQUEIDENTIFIER NULL,
        UnitCost       DECIMAL(19,4)    NOT NULL CONSTRAINT DF_GoodsReceiptLines_UnitCost DEFAULT 0,
        BatchId        UNIQUEIDENTIFIER NULL,
        BinId          UNIQUEIDENTIFIER NULL,
        CONSTRAINT CK_GoodsReceiptLines_Qty CHECK (Qty > 0)
    );
END
GO

IF OBJECT_ID(N'inventory.MaterialRequisitions', N'U') IS NULL
BEGIN
    CREATE TABLE inventory.MaterialRequisitions
    (
        Id             UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_MaterialRequisitions PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        OrganizationId UNIQUEIDENTIFIER NOT NULL,
        ReqNumber      NVARCHAR(50)     NOT NULL,
        WarehouseId    UNIQUEIDENTIFIER NOT NULL,
        ProjectId      UNIQUEIDENTIFIER NULL,
        WorkOrderId    UNIQUEIDENTIFIER NULL,
        Status         NVARCHAR(30)     NOT NULL CONSTRAINT DF_MaterialRequisitions_Status DEFAULT N'DRAFT',
        RequestedBy    UNIQUEIDENTIFIER NULL,
        Notes          NVARCHAR(1000)   NULL,
        CreatedAt      DATETIME2        NOT NULL CONSTRAINT DF_MaterialRequisitions_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt      DATETIME2        NULL,
        CreatedBy      UNIQUEIDENTIFIER NULL,
        UpdatedBy      UNIQUEIDENTIFIER NULL,
        CONSTRAINT UQ_MaterialRequisitions_Org_No UNIQUE (OrganizationId, ReqNumber)
    );
END
GO

IF OBJECT_ID(N'inventory.MaterialRequisitionLines', N'U') IS NULL
BEGIN
    CREATE TABLE inventory.MaterialRequisitionLines
    (
        Id                    UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_MaterialRequisitionLines PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        MaterialRequisitionId UNIQUEIDENTIFIER NOT NULL,
        ItemId                UNIQUEIDENTIFIER NOT NULL,
        QtyRequested          DECIMAL(19,4)    NOT NULL,
        QtyIssued             DECIMAL(19,4)    NOT NULL CONSTRAINT DF_MaterialRequisitionLines_Issued DEFAULT 0,
        UomId                 UNIQUEIDENTIFIER NULL
    );
END
GO

IF OBJECT_ID(N'inventory.MaterialIssues', N'U') IS NULL
BEGIN
    CREATE TABLE inventory.MaterialIssues
    (
        Id             UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_MaterialIssues PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        OrganizationId UNIQUEIDENTIFIER NOT NULL,
        IssueNumber    NVARCHAR(50)     NOT NULL,
        WarehouseId    UNIQUEIDENTIFIER NOT NULL,
        ProjectId      UNIQUEIDENTIFIER NULL,
        WorkOrderId    UNIQUEIDENTIFIER NULL,
        IssueType      NVARCHAR(30)     NOT NULL CONSTRAINT DF_MaterialIssues_Type DEFAULT N'PROJECT',
        IssueDate      DATE             NULL,
        Status         NVARCHAR(30)     NOT NULL CONSTRAINT DF_MaterialIssues_Status DEFAULT N'POSTED',
        Notes          NVARCHAR(1000)   NULL,
        CreatedAt      DATETIME2        NOT NULL CONSTRAINT DF_MaterialIssues_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt      DATETIME2        NULL,
        CreatedBy      UNIQUEIDENTIFIER NULL,
        UpdatedBy      UNIQUEIDENTIFIER NULL,
        CONSTRAINT UQ_MaterialIssues_Org_No UNIQUE (OrganizationId, IssueNumber)
    );
END
GO

IF OBJECT_ID(N'inventory.MaterialIssueLines', N'U') IS NULL
BEGIN
    CREATE TABLE inventory.MaterialIssueLines
    (
        Id              UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_MaterialIssueLines PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        MaterialIssueId UNIQUEIDENTIFIER NOT NULL,
        ItemId          UNIQUEIDENTIFIER NOT NULL,
        Qty             DECIMAL(19,4)    NOT NULL,
        UomId           UNIQUEIDENTIFIER NULL,
        BatchId         UNIQUEIDENTIFIER NULL,
        UnitCost        DECIMAL(19,4)    NOT NULL CONSTRAINT DF_MaterialIssueLines_UnitCost DEFAULT 0,
        CONSTRAINT CK_MaterialIssueLines_Qty CHECK (Qty > 0)
    );
END
GO

IF OBJECT_ID(N'inventory.StockTransfers', N'U') IS NULL
BEGIN
    CREATE TABLE inventory.StockTransfers
    (
        Id              UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_StockTransfers PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        OrganizationId  UNIQUEIDENTIFIER NOT NULL,
        TransferNumber  NVARCHAR(50)     NOT NULL,
        FromWarehouseId UNIQUEIDENTIFIER NOT NULL,
        ToWarehouseId   UNIQUEIDENTIFIER NOT NULL,
        TransportId     NVARCHAR(50)     NULL,
        Status          NVARCHAR(30)     NOT NULL CONSTRAINT DF_StockTransfers_Status DEFAULT N'POSTED',
        TransferDate    DATE             NULL,
        Notes           NVARCHAR(1000)   NULL,
        CreatedAt       DATETIME2        NOT NULL CONSTRAINT DF_StockTransfers_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt       DATETIME2        NULL,
        CreatedBy       UNIQUEIDENTIFIER NULL,
        UpdatedBy       UNIQUEIDENTIFIER NULL,
        CONSTRAINT UQ_StockTransfers_Org_No UNIQUE (OrganizationId, TransferNumber),
        CONSTRAINT CK_StockTransfers_Warehouses CHECK (FromWarehouseId <> ToWarehouseId)
    );
END
GO

IF OBJECT_ID(N'inventory.StockTransferLines', N'U') IS NULL
BEGIN
    CREATE TABLE inventory.StockTransferLines
    (
        Id              UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_StockTransferLines PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        StockTransferId UNIQUEIDENTIFIER NOT NULL,
        ItemId          UNIQUEIDENTIFIER NOT NULL,
        Qty             DECIMAL(19,4)    NOT NULL,
        UomId           UNIQUEIDENTIFIER NULL,
        BatchId         UNIQUEIDENTIFIER NULL,
        CONSTRAINT CK_StockTransferLines_Qty CHECK (Qty > 0)
    );
END
GO

IF OBJECT_ID(N'inventory.StockAdjustments', N'U') IS NULL
BEGIN
    CREATE TABLE inventory.StockAdjustments
    (
        Id               UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_StockAdjustments PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        OrganizationId   UNIQUEIDENTIFIER NOT NULL,
        AdjustmentNumber NVARCHAR(50)     NOT NULL,
        WarehouseId      UNIQUEIDENTIFIER NOT NULL,
        ReasonCode       NVARCHAR(50)     NOT NULL,
        AdjustmentDate   DATE             NULL,
        Status           NVARCHAR(30)     NOT NULL CONSTRAINT DF_StockAdjustments_Status DEFAULT N'POSTED',
        Notes            NVARCHAR(1000)   NULL,
        CreatedAt        DATETIME2        NOT NULL CONSTRAINT DF_StockAdjustments_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt        DATETIME2        NULL,
        CreatedBy        UNIQUEIDENTIFIER NULL,
        UpdatedBy        UNIQUEIDENTIFIER NULL,
        CONSTRAINT UQ_StockAdjustments_Org_No UNIQUE (OrganizationId, AdjustmentNumber)
    );
END
GO

IF OBJECT_ID(N'inventory.StockAdjustmentLines', N'U') IS NULL
BEGIN
    CREATE TABLE inventory.StockAdjustmentLines
    (
        Id                UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_StockAdjustmentLines PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        StockAdjustmentId UNIQUEIDENTIFIER NOT NULL,
        ItemId            UNIQUEIDENTIFIER NOT NULL,
        QtyDelta          DECIMAL(19,4)    NOT NULL,
        UomId             UNIQUEIDENTIFIER NULL,
        BatchId           UNIQUEIDENTIFIER NULL,
        UnitCost          DECIMAL(19,4)    NOT NULL CONSTRAINT DF_StockAdjustmentLines_UnitCost DEFAULT 0,
        CONSTRAINT CK_StockAdjustmentLines_Qty CHECK (QtyDelta <> 0)
    );
END
GO

IF OBJECT_ID(N'inventory.Returns', N'U') IS NULL
BEGIN
    CREATE TABLE inventory.Returns
    (
        Id             UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Returns PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        OrganizationId UNIQUEIDENTIFIER NOT NULL,
        ReturnNumber   NVARCHAR(50)     NOT NULL,
        ReturnType     NVARCHAR(30)     NOT NULL,
        SupplierId     UNIQUEIDENTIFIER NULL,
        WarehouseId    UNIQUEIDENTIFIER NOT NULL,
        ProjectId      UNIQUEIDENTIFIER NULL,
        ReturnDate     DATE             NULL,
        Status         NVARCHAR(30)     NOT NULL CONSTRAINT DF_Returns_Status DEFAULT N'POSTED',
        Notes          NVARCHAR(1000)   NULL,
        CreatedAt      DATETIME2        NOT NULL CONSTRAINT DF_Returns_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt      DATETIME2        NULL,
        CreatedBy      UNIQUEIDENTIFIER NULL,
        UpdatedBy      UNIQUEIDENTIFIER NULL,
        CONSTRAINT UQ_Returns_Org_No UNIQUE (OrganizationId, ReturnNumber),
        CONSTRAINT CK_Returns_Type CHECK (ReturnType IN (N'SUPPLIER', N'SITE'))
    );
END
GO

IF OBJECT_ID(N'inventory.ReturnLines', N'U') IS NULL
BEGIN
    CREATE TABLE inventory.ReturnLines
    (
        Id       UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ReturnLines PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        ReturnId UNIQUEIDENTIFIER NOT NULL,
        ItemId   UNIQUEIDENTIFIER NOT NULL,
        Qty      DECIMAL(19,4)    NOT NULL,
        UomId    UNIQUEIDENTIFIER NULL,
        BatchId  UNIQUEIDENTIFIER NULL,
        UnitCost DECIMAL(19,4)    NOT NULL CONSTRAINT DF_ReturnLines_UnitCost DEFAULT 0,
        CONSTRAINT CK_ReturnLines_Qty CHECK (Qty > 0)
    );
END
GO

/* -----------------------------------------------------------------------------
   MANUFACTURING
   -------------------------------------------------------------------------- */

IF OBJECT_ID(N'inventory.BillsOfMaterials', N'U') IS NULL
BEGIN
    CREATE TABLE inventory.BillsOfMaterials
    (
        Id             UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_BillsOfMaterials PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        OrganizationId UNIQUEIDENTIFIER NOT NULL,
        Code           NVARCHAR(50)     NOT NULL,
        OutputItemId   UNIQUEIDENTIFIER NOT NULL,
        OutputQty      DECIMAL(19,4)    NOT NULL CONSTRAINT DF_BillsOfMaterials_OutputQty DEFAULT 1,
        UomId          UNIQUEIDENTIFIER NULL,
        Version        INT              NOT NULL CONSTRAINT DF_BillsOfMaterials_Version DEFAULT 1,
        IsActive       BIT              NOT NULL CONSTRAINT DF_BillsOfMaterials_IsActive DEFAULT 1,
        CreatedAt      DATETIME2        NOT NULL CONSTRAINT DF_BillsOfMaterials_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt      DATETIME2        NULL,
        CreatedBy      UNIQUEIDENTIFIER NULL,
        UpdatedBy      UNIQUEIDENTIFIER NULL,
        CONSTRAINT UQ_BillsOfMaterials_Org_Code UNIQUE (OrganizationId, Code)
    );
END
GO

IF OBJECT_ID(N'inventory.BomLines', N'U') IS NULL
BEGIN
    CREATE TABLE inventory.BomLines
    (
        Id                UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_BomLines PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        BillOfMaterialsId UNIQUEIDENTIFIER NOT NULL,
        ComponentItemId   UNIQUEIDENTIFIER NOT NULL,
        Qty               DECIMAL(19,4)    NOT NULL,
        UomId             UNIQUEIDENTIFIER NULL,
        ScrapPercent      DECIMAL(9,4)     NOT NULL CONSTRAINT DF_BomLines_Scrap DEFAULT 0,
        Operation         NVARCHAR(100)    NULL
    );
END
GO

IF OBJECT_ID(N'inventory.WorkOrders', N'U') IS NULL
BEGIN
    CREATE TABLE inventory.WorkOrders
    (
        Id             UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_WorkOrders PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        OrganizationId UNIQUEIDENTIFIER NOT NULL,
        WoNumber       NVARCHAR(50)     NOT NULL,
        OutputItemId   UNIQUEIDENTIFIER NOT NULL,
        BomId          UNIQUEIDENTIFIER NULL,
        PlannedQty     DECIMAL(19,4)    NOT NULL,
        ProducedQty    DECIMAL(19,4)    NOT NULL CONSTRAINT DF_WorkOrders_Produced DEFAULT 0,
        ScrapQty       DECIMAL(19,4)    NOT NULL CONSTRAINT DF_WorkOrders_Scrap DEFAULT 0,
        WarehouseId    UNIQUEIDENTIFIER NOT NULL,
        ProjectId      UNIQUEIDENTIFIER NULL,
        Status         NVARCHAR(30)     NOT NULL CONSTRAINT DF_WorkOrders_Status DEFAULT N'DRAFT',
        StartDate      DATE             NULL,
        EndDate        DATE             NULL,
        CreatedAt      DATETIME2        NOT NULL CONSTRAINT DF_WorkOrders_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt      DATETIME2        NULL,
        CreatedBy      UNIQUEIDENTIFIER NULL,
        UpdatedBy      UNIQUEIDENTIFIER NULL,
        RowVersion     ROWVERSION       NOT NULL,
        CONSTRAINT UQ_WorkOrders_Org_No UNIQUE (OrganizationId, WoNumber)
    );
END
GO

IF OBJECT_ID(N'inventory.WorkOrderComponents', N'U') IS NULL
BEGIN
    CREATE TABLE inventory.WorkOrderComponents
    (
        Id              UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_WorkOrderComponents PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        WorkOrderId     UNIQUEIDENTIFIER NOT NULL,
        ComponentItemId UNIQUEIDENTIFIER NOT NULL,
        PlannedQty      DECIMAL(19,4)    NOT NULL,
        ConsumedQty     DECIMAL(19,4)    NOT NULL CONSTRAINT DF_WorkOrderComponents_Consumed DEFAULT 0,
        UomId           UNIQUEIDENTIFIER NULL,
        BatchId         UNIQUEIDENTIFIER NULL
    );
END
GO

/* -----------------------------------------------------------------------------
   COUNTING
   -------------------------------------------------------------------------- */

IF OBJECT_ID(N'inventory.CycleCounts', N'U') IS NULL
BEGIN
    CREATE TABLE inventory.CycleCounts
    (
        Id             UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_CycleCounts PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        OrganizationId UNIQUEIDENTIFIER NOT NULL,
        CountNumber    NVARCHAR(50)     NOT NULL,
        WarehouseId    UNIQUEIDENTIFIER NOT NULL,
        Status         NVARCHAR(30)     NOT NULL CONSTRAINT DF_CycleCounts_Status DEFAULT N'DRAFT',
        CountDate      DATE             NULL,
        Notes          NVARCHAR(1000)   NULL,
        CreatedAt      DATETIME2        NOT NULL CONSTRAINT DF_CycleCounts_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt      DATETIME2        NULL,
        CreatedBy      UNIQUEIDENTIFIER NULL,
        UpdatedBy      UNIQUEIDENTIFIER NULL,
        CONSTRAINT UQ_CycleCounts_Org_No UNIQUE (OrganizationId, CountNumber)
    );
END
GO

IF OBJECT_ID(N'inventory.CycleCountLines', N'U') IS NULL
BEGIN
    CREATE TABLE inventory.CycleCountLines
    (
        Id           UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_CycleCountLines PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        CycleCountId UNIQUEIDENTIFIER NOT NULL,
        ItemId       UNIQUEIDENTIFIER NOT NULL,
        BatchId      UNIQUEIDENTIFIER NULL,
        SystemQty    DECIMAL(19,4)    NOT NULL CONSTRAINT DF_CycleCountLines_SystemQty DEFAULT 0,
        CountedQty   DECIMAL(19,4)    NOT NULL CONSTRAINT DF_CycleCountLines_CountedQty DEFAULT 0,
        VarianceQty  AS (CountedQty - SystemQty) PERSISTED
    );
END
GO

/* =============================================================================
   FOREIGN KEYS (ON DELETE NO ACTION). Added after all tables exist.
   ========================================================================== */

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Inv_ItemCategories_Organization')
    ALTER TABLE inventory.ItemCategories ADD CONSTRAINT FK_Inv_ItemCategories_Organization
        FOREIGN KEY (OrganizationId) REFERENCES core.Organizations (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Inv_ItemCategories_Parent')
    ALTER TABLE inventory.ItemCategories ADD CONSTRAINT FK_Inv_ItemCategories_Parent
        FOREIGN KEY (ParentCategoryId) REFERENCES inventory.ItemCategories (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Inv_Uom_Organization')
    ALTER TABLE inventory.UnitsOfMeasure ADD CONSTRAINT FK_Inv_Uom_Organization
        FOREIGN KEY (OrganizationId) REFERENCES core.Organizations (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Inv_UomConversions_From')
    ALTER TABLE inventory.UomConversions ADD CONSTRAINT FK_Inv_UomConversions_From
        FOREIGN KEY (FromUomId) REFERENCES inventory.UnitsOfMeasure (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Inv_UomConversions_To')
    ALTER TABLE inventory.UomConversions ADD CONSTRAINT FK_Inv_UomConversions_To
        FOREIGN KEY (ToUomId) REFERENCES inventory.UnitsOfMeasure (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Inv_Items_Organization')
    ALTER TABLE inventory.Items ADD CONSTRAINT FK_Inv_Items_Organization
        FOREIGN KEY (OrganizationId) REFERENCES core.Organizations (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Inv_Items_Category')
    ALTER TABLE inventory.Items ADD CONSTRAINT FK_Inv_Items_Category
        FOREIGN KEY (CategoryId) REFERENCES inventory.ItemCategories (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Inv_Items_BaseUom')
    ALTER TABLE inventory.Items ADD CONSTRAINT FK_Inv_Items_BaseUom
        FOREIGN KEY (BaseUomId) REFERENCES inventory.UnitsOfMeasure (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Inv_Warehouses_Organization')
    ALTER TABLE inventory.Warehouses ADD CONSTRAINT FK_Inv_Warehouses_Organization
        FOREIGN KEY (OrganizationId) REFERENCES core.Organizations (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Inv_Warehouses_Project')
    ALTER TABLE inventory.Warehouses ADD CONSTRAINT FK_Inv_Warehouses_Project
        FOREIGN KEY (ProjectId) REFERENCES project.Projects (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Inv_StorageBins_Warehouse')
    ALTER TABLE inventory.StorageBins ADD CONSTRAINT FK_Inv_StorageBins_Warehouse
        FOREIGN KEY (WarehouseId) REFERENCES inventory.Warehouses (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Inv_Suppliers_Organization')
    ALTER TABLE inventory.Suppliers ADD CONSTRAINT FK_Inv_Suppliers_Organization
        FOREIGN KEY (OrganizationId) REFERENCES core.Organizations (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Inv_ItemSuppliers_Item')
    ALTER TABLE inventory.ItemSuppliers ADD CONSTRAINT FK_Inv_ItemSuppliers_Item
        FOREIGN KEY (ItemId) REFERENCES inventory.Items (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Inv_ItemSuppliers_Supplier')
    ALTER TABLE inventory.ItemSuppliers ADD CONSTRAINT FK_Inv_ItemSuppliers_Supplier
        FOREIGN KEY (SupplierId) REFERENCES inventory.Suppliers (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Inv_Batches_Item')
    ALTER TABLE inventory.Batches ADD CONSTRAINT FK_Inv_Batches_Item
        FOREIGN KEY (ItemId) REFERENCES inventory.Items (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Inv_Batches_Supplier')
    ALTER TABLE inventory.Batches ADD CONSTRAINT FK_Inv_Batches_Supplier
        FOREIGN KEY (SupplierId) REFERENCES inventory.Suppliers (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Inv_SerialNumbers_Item')
    ALTER TABLE inventory.SerialNumbers ADD CONSTRAINT FK_Inv_SerialNumbers_Item
        FOREIGN KEY (ItemId) REFERENCES inventory.Items (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Inv_SerialNumbers_Warehouse')
    ALTER TABLE inventory.SerialNumbers ADD CONSTRAINT FK_Inv_SerialNumbers_Warehouse
        FOREIGN KEY (WarehouseId) REFERENCES inventory.Warehouses (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Inv_StockLevels_Item')
    ALTER TABLE inventory.StockLevels ADD CONSTRAINT FK_Inv_StockLevels_Item
        FOREIGN KEY (ItemId) REFERENCES inventory.Items (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Inv_StockLevels_Warehouse')
    ALTER TABLE inventory.StockLevels ADD CONSTRAINT FK_Inv_StockLevels_Warehouse
        FOREIGN KEY (WarehouseId) REFERENCES inventory.Warehouses (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Inv_StockLevels_Batch')
    ALTER TABLE inventory.StockLevels ADD CONSTRAINT FK_Inv_StockLevels_Batch
        FOREIGN KEY (BatchId) REFERENCES inventory.Batches (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Inv_StockMovements_Item')
    ALTER TABLE inventory.StockMovements ADD CONSTRAINT FK_Inv_StockMovements_Item
        FOREIGN KEY (ItemId) REFERENCES inventory.Items (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Inv_StockMovements_Warehouse')
    ALTER TABLE inventory.StockMovements ADD CONSTRAINT FK_Inv_StockMovements_Warehouse
        FOREIGN KEY (WarehouseId) REFERENCES inventory.Warehouses (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Inv_PurchaseOrders_Supplier')
    ALTER TABLE inventory.PurchaseOrders ADD CONSTRAINT FK_Inv_PurchaseOrders_Supplier
        FOREIGN KEY (SupplierId) REFERENCES inventory.Suppliers (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Inv_PurchaseOrderLines_PO')
    ALTER TABLE inventory.PurchaseOrderLines ADD CONSTRAINT FK_Inv_PurchaseOrderLines_PO
        FOREIGN KEY (PurchaseOrderId) REFERENCES inventory.PurchaseOrders (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Inv_GoodsReceipts_Warehouse')
    ALTER TABLE inventory.GoodsReceipts ADD CONSTRAINT FK_Inv_GoodsReceipts_Warehouse
        FOREIGN KEY (WarehouseId) REFERENCES inventory.Warehouses (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Inv_GoodsReceiptLines_GRN')
    ALTER TABLE inventory.GoodsReceiptLines ADD CONSTRAINT FK_Inv_GoodsReceiptLines_GRN
        FOREIGN KEY (GoodsReceiptId) REFERENCES inventory.GoodsReceipts (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Inv_MaterialIssues_Warehouse')
    ALTER TABLE inventory.MaterialIssues ADD CONSTRAINT FK_Inv_MaterialIssues_Warehouse
        FOREIGN KEY (WarehouseId) REFERENCES inventory.Warehouses (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Inv_MaterialIssueLines_Issue')
    ALTER TABLE inventory.MaterialIssueLines ADD CONSTRAINT FK_Inv_MaterialIssueLines_Issue
        FOREIGN KEY (MaterialIssueId) REFERENCES inventory.MaterialIssues (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Inv_MaterialReqLines_Req')
    ALTER TABLE inventory.MaterialRequisitionLines ADD CONSTRAINT FK_Inv_MaterialReqLines_Req
        FOREIGN KEY (MaterialRequisitionId) REFERENCES inventory.MaterialRequisitions (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Inv_StockTransferLines_Transfer')
    ALTER TABLE inventory.StockTransferLines ADD CONSTRAINT FK_Inv_StockTransferLines_Transfer
        FOREIGN KEY (StockTransferId) REFERENCES inventory.StockTransfers (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Inv_StockAdjustmentLines_Adj')
    ALTER TABLE inventory.StockAdjustmentLines ADD CONSTRAINT FK_Inv_StockAdjustmentLines_Adj
        FOREIGN KEY (StockAdjustmentId) REFERENCES inventory.StockAdjustments (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Inv_ReturnLines_Return')
    ALTER TABLE inventory.ReturnLines ADD CONSTRAINT FK_Inv_ReturnLines_Return
        FOREIGN KEY (ReturnId) REFERENCES inventory.Returns (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Inv_BomLines_Bom')
    ALTER TABLE inventory.BomLines ADD CONSTRAINT FK_Inv_BomLines_Bom
        FOREIGN KEY (BillOfMaterialsId) REFERENCES inventory.BillsOfMaterials (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Inv_WorkOrderComponents_WO')
    ALTER TABLE inventory.WorkOrderComponents ADD CONSTRAINT FK_Inv_WorkOrderComponents_WO
        FOREIGN KEY (WorkOrderId) REFERENCES inventory.WorkOrders (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Inv_CycleCountLines_Count')
    ALTER TABLE inventory.CycleCountLines ADD CONSTRAINT FK_Inv_CycleCountLines_Count
        FOREIGN KEY (CycleCountId) REFERENCES inventory.CycleCounts (Id);
GO

/* =============================================================================
   INDEXES
   ========================================================================== */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Inv_Items_Org_Type' AND object_id = OBJECT_ID(N'inventory.Items'))
    CREATE INDEX IX_Inv_Items_Org_Type ON inventory.Items (OrganizationId, ItemType) WHERE IsDeleted = 0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Inv_Items_Org_Category' AND object_id = OBJECT_ID(N'inventory.Items'))
    CREATE INDEX IX_Inv_Items_Org_Category ON inventory.Items (OrganizationId, CategoryId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Inv_StockLevels_Org_Item' AND object_id = OBJECT_ID(N'inventory.StockLevels'))
    CREATE INDEX IX_Inv_StockLevels_Org_Item ON inventory.StockLevels (OrganizationId, ItemId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Inv_StockLevels_Warehouse' AND object_id = OBJECT_ID(N'inventory.StockLevels'))
    CREATE INDEX IX_Inv_StockLevels_Warehouse ON inventory.StockLevels (WarehouseId, ItemId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Inv_StockMovements_Org_Item_Date' AND object_id = OBJECT_ID(N'inventory.StockMovements'))
    CREATE INDEX IX_Inv_StockMovements_Org_Item_Date ON inventory.StockMovements (OrganizationId, ItemId, OccurredAt);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Inv_StockMovements_Project' AND object_id = OBJECT_ID(N'inventory.StockMovements'))
    CREATE INDEX IX_Inv_StockMovements_Project ON inventory.StockMovements (ProjectId, ItemId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Inv_Batches_Expiry' AND object_id = OBJECT_ID(N'inventory.Batches'))
    CREATE INDEX IX_Inv_Batches_Expiry ON inventory.Batches (OrganizationId, ExpiryDate);
GO

/* =============================================================================
   ADDITIVE COLUMNS (idempotent) — for DBs created before these were added.
   ========================================================================== */
IF COL_LENGTH(N'inventory.GoodsReceipts', N'PoReference') IS NULL
    ALTER TABLE inventory.GoodsReceipts ADD PoReference NVARCHAR(50) NULL;
IF COL_LENGTH(N'inventory.StockTransfers', N'TransportId') IS NULL
    ALTER TABLE inventory.StockTransfers ADD TransportId NVARCHAR(50) NULL;
GO

/* =============================================================================
   SEED: Inventory permissions (global, tenant-independent). Idempotent by Code.
   ========================================================================== */
INSERT INTO core.Permissions (Code, Name, Module, Resource, Action)
SELECT v.Code, v.Name, v.Module, v.Resource, v.Action
FROM (VALUES
    (N'item.read',           N'View Items',              N'inventory', N'item',          N'read'),
    (N'item.create',         N'Create Items',            N'inventory', N'item',          N'create'),
    (N'item.update',         N'Update Items',            N'inventory', N'item',          N'update'),
    (N'item.delete',         N'Delete Items',            N'inventory', N'item',          N'delete'),
    (N'warehouse.read',      N'View Warehouses',         N'inventory', N'warehouse',     N'read'),
    (N'warehouse.create',    N'Create Warehouses',       N'inventory', N'warehouse',     N'create'),
    (N'warehouse.update',    N'Update Warehouses',       N'inventory', N'warehouse',     N'update'),
    (N'warehouse.delete',    N'Delete Warehouses',       N'inventory', N'warehouse',     N'delete'),
    (N'supplier.read',       N'View Suppliers',          N'inventory', N'supplier',      N'read'),
    (N'supplier.create',     N'Create Suppliers',        N'inventory', N'supplier',      N'create'),
    (N'supplier.update',     N'Update Suppliers',        N'inventory', N'supplier',      N'update'),
    (N'supplier.delete',     N'Delete Suppliers',        N'inventory', N'supplier',      N'delete'),
    (N'stock.read',          N'View Stock',              N'inventory', N'stock',         N'read'),
    (N'stock.adjust',        N'Adjust Stock',            N'inventory', N'stock',         N'adjust'),
    (N'goodsreceipt.read',   N'View Goods Receipts',     N'inventory', N'goodsreceipt',  N'read'),
    (N'goodsreceipt.create', N'Create Goods Receipts',   N'inventory', N'goodsreceipt',  N'create'),
    (N'issue.read',          N'View Material Issues',    N'inventory', N'issue',         N'read'),
    (N'issue.create',        N'Create Material Issues',  N'inventory', N'issue',         N'create'),
    (N'transfer.read',       N'View Stock Transfers',    N'inventory', N'transfer',      N'read'),
    (N'transfer.create',     N'Create Stock Transfers',  N'inventory', N'transfer',      N'create'),
    (N'bom.read',            N'View BOMs',               N'inventory', N'bom',           N'read'),
    (N'bom.create',          N'Create BOMs',             N'inventory', N'bom',           N'create'),
    (N'bom.update',          N'Update BOMs',             N'inventory', N'bom',           N'update'),
    (N'bom.delete',          N'Delete BOMs',             N'inventory', N'bom',           N'delete'),
    (N'workorder.read',      N'View Work Orders',        N'inventory', N'workorder',     N'read'),
    (N'workorder.create',    N'Create Work Orders',      N'inventory', N'workorder',     N'create'),
    (N'workorder.release',   N'Release Work Orders',     N'inventory', N'workorder',     N'release'),
    (N'workorder.complete',  N'Complete Work Orders',    N'inventory', N'workorder',     N'complete'),
    (N'inventory.report.read', N'View Inventory Reports', N'inventory', N'report',       N'read')
) AS v(Code, Name, Module, Resource, Action)
WHERE NOT EXISTS (SELECT 1 FROM core.Permissions p WHERE p.Code = v.Code);
GO

/* =============================================================================
   PROCEDURE: inventory.usp_SeedOrganizationInventoryDefaults
   Idempotently seeds default UOMs and item categories for an organization,
   and grants inventory permissions to SUPER_ADMIN / ADMIN / VIEWER roles.
   ========================================================================== */
CREATE OR ALTER PROCEDURE inventory.usp_SeedOrganizationInventoryDefaults
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM core.Organizations WHERE Id = @OrganizationId)
        THROW 50001, N'Organization does not exist.', 1;

    -- Units of measure
    INSERT INTO inventory.UnitsOfMeasure (OrganizationId, Code, Name, IsBaseUnit)
    SELECT @OrganizationId, v.Code, v.Name, v.IsBaseUnit
    FROM (VALUES
        (N'NOS', N'Numbers', 1),
        (N'KG',  N'Kilogram', 1),
        (N'TON', N'Metric Ton', 0),
        (N'BAG', N'Bag', 0),
        (N'LTR', N'Litre', 1),
        (N'M',   N'Metre', 1),
        (N'M2',  N'Square Metre', 1),
        (N'M3',  N'Cubic Metre', 1),
        (N'BOX', N'Box', 0)
    ) AS v(Code, Name, IsBaseUnit)
    WHERE NOT EXISTS (SELECT 1 FROM inventory.UnitsOfMeasure u WHERE u.OrganizationId = @OrganizationId AND u.Code = v.Code);

    -- Item categories
    INSERT INTO inventory.ItemCategories (OrganizationId, Code, Name)
    SELECT @OrganizationId, v.Code, v.Name
    FROM (VALUES
        (N'RAW',       N'Raw Materials'),
        (N'FINISHED',  N'Finished Products'),
        (N'CONSUMABLE',N'Consumables'),
        (N'SPARE',     N'Spare Parts'),
        (N'TOOL',      N'Tools & Equipment')
    ) AS v(Code, Name)
    WHERE NOT EXISTS (SELECT 1 FROM inventory.ItemCategories c WHERE c.OrganizationId = @OrganizationId AND c.Code = v.Code);

    -- Grant inventory permissions to existing admin + viewer roles
    ;WITH RoleMap AS (
        SELECT r.Id AS RoleId, r.Name AS RoleName FROM core.Roles r WHERE r.OrganizationId = @OrganizationId
    ),
    Grants AS (
        SELECT rm.RoleId, p.Id AS PermissionId
        FROM RoleMap rm CROSS JOIN core.Permissions p
        WHERE rm.RoleName IN (N'SUPER_ADMIN', N'ADMIN') AND p.Module = N'inventory'
        UNION
        SELECT rm.RoleId, p.Id
        FROM RoleMap rm JOIN core.Permissions p ON p.Module = N'inventory' AND p.Action = N'read'
        WHERE rm.RoleName = N'VIEWER'
    )
    INSERT INTO core.RolePermissions (RoleId, PermissionId)
    SELECT g.RoleId, g.PermissionId
    FROM Grants g
    WHERE NOT EXISTS (
        SELECT 1 FROM core.RolePermissions rp WHERE rp.RoleId = g.RoleId AND rp.PermissionId = g.PermissionId
    );
END
GO
