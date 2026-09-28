SET NOCOUNT ON;
BEGIN TRANSACTION;

IF COL_LENGTH(N'inventory.Items', N'ValuationMethod') IS NOT NULL
BEGIN
    DECLARE @valCk sysname = (SELECT name FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID(N'inventory.Items') AND name = N'CK_Items_Valuation');
    IF @valCk IS NOT NULL EXEC('ALTER TABLE inventory.Items DROP CONSTRAINT ' + @valCk);

    DECLARE @valDf sysname = (SELECT d.name FROM sys.default_constraints d JOIN sys.columns c ON d.parent_object_id = c.object_id AND d.parent_column_id = c.column_id WHERE d.parent_object_id = OBJECT_ID(N'inventory.Items') AND c.name = N'ValuationMethod');
    IF @valDf IS NOT NULL EXEC('ALTER TABLE inventory.Items DROP CONSTRAINT ' + @valDf);

    ALTER TABLE inventory.Items DROP COLUMN ValuationMethod;
    PRINT 'Dropped inventory.Items.ValuationMethod';
END
ELSE
BEGIN
    PRINT 'ValuationMethod column already absent';
END

COMMIT TRANSACTION;

SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'inventory' AND TABLE_NAME = 'Items' ORDER BY ORDINAL_POSITION;
