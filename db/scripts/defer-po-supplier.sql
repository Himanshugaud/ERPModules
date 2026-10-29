SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'inventory.PurchaseOrders')
      AND name = N'SupplierId' AND is_nullable = 0
)
    ALTER TABLE inventory.PurchaseOrders ALTER COLUMN SupplierId UNIQUEIDENTIFIER NULL;
COMMIT TRANSACTION;