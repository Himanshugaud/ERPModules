/* =============================================================================
   ERP — PROCUREMENT MODULE SCHEMA (additive, lives in the existing `inventory` schema)
   Idempotent, GO-batched. Apply AFTER inventory-schema.sql.
   Adds Material Requirements + extends PurchaseOrders/StockTransfers for full lifecycles.
   ========================================================================== */

/* -----------------------------------------------------------------------------
   NEW TABLES — Material Requirements (Planning -> Procurement)
   -------------------------------------------------------------------------- */

IF OBJECT_ID(N'inventory.MaterialRequirements', N'U') IS NULL
BEGIN
    CREATE TABLE inventory.MaterialRequirements
    (
        Id               UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_MaterialRequirements PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        OrganizationId   UNIQUEIDENTIFIER NOT NULL,
        ReqNumber        NVARCHAR(50)     NOT NULL,
        ProjectId        UNIQUEIDENTIFIER NOT NULL,
        WarehouseId      UNIQUEIDENTIFIER NULL,
        DestinationAddress NVARCHAR(300)  NULL,
        DepartmentId     UNIQUEIDENTIFIER NULL,
        Priority         NVARCHAR(20)     NOT NULL CONSTRAINT DF_MaterialRequirements_Priority DEFAULT N'MEDIUM',
        Status           NVARCHAR(30)     NOT NULL CONSTRAINT DF_MaterialRequirements_Status DEFAULT N'SUBMITTED',
        RequiredDate     DATE             NULL,
        RequestedBy      UNIQUEIDENTIFIER NULL,
        ApprovedBy       UNIQUEIDENTIFIER NULL,
        ApprovedAt       DATETIME2        NULL,
        RejectedBy       UNIQUEIDENTIFIER NULL,
        RejectedAt       DATETIME2        NULL,
        RejectionReason  NVARCHAR(500)    NULL,
        PurchaseOrderId  UNIQUEIDENTIFIER NULL,
        Notes            NVARCHAR(1000)   NULL,
        CreatedAt        DATETIME2        NOT NULL CONSTRAINT DF_MaterialRequirements_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt        DATETIME2        NULL,
        CreatedBy        UNIQUEIDENTIFIER NULL,
        UpdatedBy        UNIQUEIDENTIFIER NULL,
        RowVersion       ROWVERSION       NOT NULL,
        CONSTRAINT UQ_MaterialRequirements_Org_No UNIQUE (OrganizationId, ReqNumber),
        CONSTRAINT CK_MaterialRequirements_Priority CHECK (Priority IN (N'LOW', N'MEDIUM', N'HIGH', N'URGENT')),
        CONSTRAINT CK_MaterialRequirements_Status CHECK (Status IN
            (N'SUBMITTED', N'APPROVED', N'REJECTED', N'CONVERTED', N'CANCELLED'))
    );
END
GO

IF OBJECT_ID(N'inventory.MaterialRequirementLines', N'U') IS NULL
BEGIN
    CREATE TABLE inventory.MaterialRequirementLines
    (
        Id                     UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_MaterialRequirementLines PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        MaterialRequirementId  UNIQUEIDENTIFIER NOT NULL,
        ItemId                 UNIQUEIDENTIFIER NOT NULL,
        Qty                    DECIMAL(19,4)    NOT NULL,
        UomId                  UNIQUEIDENTIFIER NULL,
        Notes                  NVARCHAR(500)    NULL,
        PurchaseOrderLineId    UNIQUEIDENTIFIER NULL,
        CONSTRAINT CK_MaterialRequirementLines_Qty CHECK (Qty > 0)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Inv_MaterialRequirements_Org')
    ALTER TABLE inventory.MaterialRequirements ADD CONSTRAINT FK_Inv_MaterialRequirements_Org
        FOREIGN KEY (OrganizationId) REFERENCES core.Organizations (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Inv_MaterialRequirements_Project')
    ALTER TABLE inventory.MaterialRequirements ADD CONSTRAINT FK_Inv_MaterialRequirements_Project
        FOREIGN KEY (ProjectId) REFERENCES project.Projects (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Inv_MaterialRequirements_Warehouse')
    ALTER TABLE inventory.MaterialRequirements ADD CONSTRAINT FK_Inv_MaterialRequirements_Warehouse
        FOREIGN KEY (WarehouseId) REFERENCES inventory.Warehouses (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Inv_MaterialRequirements_Department')
    ALTER TABLE inventory.MaterialRequirements ADD CONSTRAINT FK_Inv_MaterialRequirements_Department
        FOREIGN KEY (DepartmentId) REFERENCES core.Departments (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Inv_MaterialRequirements_PO')
    ALTER TABLE inventory.MaterialRequirements ADD CONSTRAINT FK_Inv_MaterialRequirements_PO
        FOREIGN KEY (PurchaseOrderId) REFERENCES inventory.PurchaseOrders (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Inv_MaterialRequirementLines_Req')
    ALTER TABLE inventory.MaterialRequirementLines ADD CONSTRAINT FK_Inv_MaterialRequirementLines_Req
        FOREIGN KEY (MaterialRequirementId) REFERENCES inventory.MaterialRequirements (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Inv_MaterialRequirementLines_Item')
    ALTER TABLE inventory.MaterialRequirementLines ADD CONSTRAINT FK_Inv_MaterialRequirementLines_Item
        FOREIGN KEY (ItemId) REFERENCES inventory.Items (Id);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Inv_MaterialRequirements_Org_Status' AND object_id = OBJECT_ID(N'inventory.MaterialRequirements'))
    CREATE INDEX IX_Inv_MaterialRequirements_Org_Status ON inventory.MaterialRequirements (OrganizationId, Status);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Inv_MaterialRequirements_Project' AND object_id = OBJECT_ID(N'inventory.MaterialRequirements'))
    CREATE INDEX IX_Inv_MaterialRequirements_Project ON inventory.MaterialRequirements (ProjectId);
GO

/* -----------------------------------------------------------------------------
   PURCHASE ORDERS — extend for full lifecycle + Planning/Site integration
   -------------------------------------------------------------------------- */

IF COL_LENGTH(N'inventory.PurchaseOrders', N'ProjectId') IS NULL
    ALTER TABLE inventory.PurchaseOrders ADD ProjectId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'inventory.PurchaseOrders', N'WarehouseId') IS NULL
    ALTER TABLE inventory.PurchaseOrders ADD WarehouseId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'inventory.PurchaseOrders', N'MaterialRequirementId') IS NULL
    ALTER TABLE inventory.PurchaseOrders ADD MaterialRequirementId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'inventory.PurchaseOrders', N'SubTotal') IS NULL
    ALTER TABLE inventory.PurchaseOrders ADD SubTotal DECIMAL(19,4) NOT NULL CONSTRAINT DF_PurchaseOrders_SubTotal DEFAULT 0;
IF COL_LENGTH(N'inventory.PurchaseOrders', N'TaxAmount') IS NULL
    ALTER TABLE inventory.PurchaseOrders ADD TaxAmount DECIMAL(19,4) NOT NULL CONSTRAINT DF_PurchaseOrders_TaxAmount DEFAULT 0;
IF COL_LENGTH(N'inventory.PurchaseOrders', N'SubmittedBy') IS NULL
    ALTER TABLE inventory.PurchaseOrders ADD SubmittedBy UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'inventory.PurchaseOrders', N'SubmittedAt') IS NULL
    ALTER TABLE inventory.PurchaseOrders ADD SubmittedAt DATETIME2 NULL;
IF COL_LENGTH(N'inventory.PurchaseOrders', N'ApprovedBy') IS NULL
    ALTER TABLE inventory.PurchaseOrders ADD ApprovedBy UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'inventory.PurchaseOrders', N'ApprovedAt') IS NULL
    ALTER TABLE inventory.PurchaseOrders ADD ApprovedAt DATETIME2 NULL;
IF COL_LENGTH(N'inventory.PurchaseOrders', N'RejectedBy') IS NULL
    ALTER TABLE inventory.PurchaseOrders ADD RejectedBy UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'inventory.PurchaseOrders', N'RejectedAt') IS NULL
    ALTER TABLE inventory.PurchaseOrders ADD RejectedAt DATETIME2 NULL;
IF COL_LENGTH(N'inventory.PurchaseOrders', N'RejectionReason') IS NULL
    ALTER TABLE inventory.PurchaseOrders ADD RejectionReason NVARCHAR(500) NULL;
IF COL_LENGTH(N'inventory.PurchaseOrders', N'ClosedAt') IS NULL
    ALTER TABLE inventory.PurchaseOrders ADD ClosedAt DATETIME2 NULL;
GO

IF COL_LENGTH(N'inventory.PurchaseOrderLines', N'TaxRatePercent') IS NULL
    ALTER TABLE inventory.PurchaseOrderLines ADD TaxRatePercent DECIMAL(9,4) NOT NULL CONSTRAINT DF_PurchaseOrderLines_TaxRate DEFAULT 0;
IF COL_LENGTH(N'inventory.PurchaseOrderLines', N'TaxAmount') IS NULL
    ALTER TABLE inventory.PurchaseOrderLines ADD TaxAmount DECIMAL(19,4) NOT NULL CONSTRAINT DF_PurchaseOrderLines_TaxAmount DEFAULT 0;
IF COL_LENGTH(N'inventory.PurchaseOrderLines', N'MaterialRequirementLineId') IS NULL
    ALTER TABLE inventory.PurchaseOrderLines ADD MaterialRequirementLineId UNIQUEIDENTIFIER NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Inv_PurchaseOrders_Project')
    ALTER TABLE inventory.PurchaseOrders ADD CONSTRAINT FK_Inv_PurchaseOrders_Project
        FOREIGN KEY (ProjectId) REFERENCES project.Projects (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Inv_PurchaseOrders_Warehouse')
    ALTER TABLE inventory.PurchaseOrders ADD CONSTRAINT FK_Inv_PurchaseOrders_Warehouse
        FOREIGN KEY (WarehouseId) REFERENCES inventory.Warehouses (Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Inv_PurchaseOrders_MaterialRequirement')
    ALTER TABLE inventory.PurchaseOrders ADD CONSTRAINT FK_Inv_PurchaseOrders_MaterialRequirement
        FOREIGN KEY (MaterialRequirementId) REFERENCES inventory.MaterialRequirements (Id);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Inv_PurchaseOrders_Org_Status' AND object_id = OBJECT_ID(N'inventory.PurchaseOrders'))
    CREATE INDEX IX_Inv_PurchaseOrders_Org_Status ON inventory.PurchaseOrders (OrganizationId, Status);
GO

/* -----------------------------------------------------------------------------
   STOCK TRANSFERS — staged lifecycle (Requested -> Approved -> Dispatched -> Received)
   -------------------------------------------------------------------------- */

IF COL_LENGTH(N'inventory.StockTransfers', N'ProjectId') IS NULL
    ALTER TABLE inventory.StockTransfers ADD ProjectId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'inventory.StockTransfers', N'RequestedBy') IS NULL
    ALTER TABLE inventory.StockTransfers ADD RequestedBy UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'inventory.StockTransfers', N'ApprovedBy') IS NULL
    ALTER TABLE inventory.StockTransfers ADD ApprovedBy UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'inventory.StockTransfers', N'ApprovedAt') IS NULL
    ALTER TABLE inventory.StockTransfers ADD ApprovedAt DATETIME2 NULL;
IF COL_LENGTH(N'inventory.StockTransfers', N'DispatchedBy') IS NULL
    ALTER TABLE inventory.StockTransfers ADD DispatchedBy UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'inventory.StockTransfers', N'DispatchedAt') IS NULL
    ALTER TABLE inventory.StockTransfers ADD DispatchedAt DATETIME2 NULL;
IF COL_LENGTH(N'inventory.StockTransfers', N'ReceivedBy') IS NULL
    ALTER TABLE inventory.StockTransfers ADD ReceivedBy UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'inventory.StockTransfers', N'ReceivedAt') IS NULL
    ALTER TABLE inventory.StockTransfers ADD ReceivedAt DATETIME2 NULL;
IF COL_LENGTH(N'inventory.StockTransfers', N'MaterialRequirementId') IS NULL
    ALTER TABLE inventory.StockTransfers ADD MaterialRequirementId UNIQUEIDENTIFIER NULL;
GO

-- Free-text destination (site address) so Planning is not locked to a preset warehouse dropdown.
IF COL_LENGTH(N'inventory.MaterialRequirements', N'DestinationAddress') IS NULL
    ALTER TABLE inventory.MaterialRequirements ADD DestinationAddress NVARCHAR(300) NULL;
GO

IF COL_LENGTH(N'inventory.StockTransferLines', N'UnitCost') IS NULL
    ALTER TABLE inventory.StockTransferLines ADD UnitCost DECIMAL(19,4) NOT NULL CONSTRAINT DF_StockTransferLines_UnitCost DEFAULT 0;
GO

/* -----------------------------------------------------------------------------
   SEED: Procurement permissions (global, tenant-independent). Idempotent by Code.
   Module = 'inventory' so the existing inventory.usp_SeedOrganizationInventoryDefaults
   proc automatically grants these to SUPER_ADMIN/ADMIN (all) and VIEWER (read).
   -------------------------------------------------------------------------- */
INSERT INTO core.Permissions (Code, Name, Module, Resource, Action)
SELECT v.Code, v.Name, v.Module, v.Resource, v.Action
FROM (VALUES
    (N'materialrequirement.read',    N'View Material Requirements',    N'inventory', N'materialrequirement', N'read'),
    (N'materialrequirement.create',  N'Create Material Requirements',  N'inventory', N'materialrequirement', N'create'),
    (N'materialrequirement.approve', N'Approve/Reject Requirements',   N'inventory', N'materialrequirement', N'approve'),
    (N'materialrequirement.convert', N'Convert Requirement to PO',     N'inventory', N'materialrequirement', N'convert'),
    (N'purchaseorder.read',          N'View Purchase Orders',          N'inventory', N'purchaseorder',       N'read'),
    (N'purchaseorder.create',        N'Create Purchase Orders',        N'inventory', N'purchaseorder',       N'create'),
    (N'purchaseorder.update',        N'Update/Submit Purchase Orders', N'inventory', N'purchaseorder',       N'update'),
    (N'purchaseorder.approve',       N'Approve/Reject Purchase Orders',N'inventory', N'purchaseorder',       N'approve'),
    (N'purchaseorder.close',         N'Close Purchase Orders',         N'inventory', N'purchaseorder',       N'close'),
    (N'transfer.approve',            N'Approve Stock Transfers',       N'inventory', N'transfer',            N'approve'),
    (N'transfer.dispatch',           N'Dispatch Stock Transfers',      N'inventory', N'transfer',            N'dispatch'),
    (N'transfer.receive',            N'Receive Stock Transfers',       N'inventory', N'transfer',            N'receive')
) AS v(Code, Name, Module, Resource, Action)
WHERE NOT EXISTS (SELECT 1 FROM core.Permissions p WHERE p.Code = v.Code);
GO
