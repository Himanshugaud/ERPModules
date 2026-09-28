SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @KeepOrganizationId UNIQUEIDENTIFIER;
DECLARE @DeletedRows INT = 0;
DECLARE @AffectedRows INT;
DECLARE @SchemaName SYSNAME;
DECLARE @TableName SYSNAME;
DECLARE @ChildColumn SYSNAME;
DECLARE @ParentSchema SYSNAME;
DECLARE @ParentTable SYSNAME;
DECLARE @ParentColumn SYSNAME;
DECLARE @Sql NVARCHAR(MAX);

SELECT @KeepOrganizationId = Id
FROM core.Organizations
WHERE Code = N'TYE';

IF @KeepOrganizationId IS NULL
    THROW 50020, N'TYE organization was not found. No data was deleted.', 1;

IF (SELECT COUNT(*) FROM core.Organizations WHERE Code = N'TYE') <> 1
    THROW 50021, N'Expected exactly one TYE organization. No data was deleted.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE DisableConstraints CURSOR LOCAL FAST_FORWARD FOR
        SELECT s.name, t.name
        FROM sys.tables t
        JOIN sys.schemas s ON s.schema_id = t.schema_id;

    OPEN DisableConstraints;
    FETCH NEXT FROM DisableConstraints INTO @SchemaName, @TableName;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @Sql = N'ALTER TABLE ' + QUOTENAME(@SchemaName) + N'.' + QUOTENAME(@TableName) +
            N' NOCHECK CONSTRAINT ALL;';
        EXEC sys.sp_executesql @Sql;
        FETCH NEXT FROM DisableConstraints INTO @SchemaName, @TableName;
    END
    CLOSE DisableConstraints;
    DEALLOCATE DisableConstraints;

    DECLARE TenantlessChildren CURSOR LOCAL FAST_FORWARD FOR
        SELECT DISTINCT
            childSchema.name,
            childTable.name,
            childColumn.name,
            parentSchema.name,
            parentTable.name,
            parentColumn.name
        FROM sys.foreign_keys fk
        JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
        JOIN sys.tables childTable ON childTable.object_id = fk.parent_object_id
        JOIN sys.schemas childSchema ON childSchema.schema_id = childTable.schema_id
        JOIN sys.columns childColumn ON childColumn.object_id = childTable.object_id
            AND childColumn.column_id = fkc.parent_column_id
        JOIN sys.tables parentTable ON parentTable.object_id = fk.referenced_object_id
        JOIN sys.schemas parentSchema ON parentSchema.schema_id = parentTable.schema_id
        JOIN sys.columns parentColumn ON parentColumn.object_id = parentTable.object_id
            AND parentColumn.column_id = fkc.referenced_column_id
        WHERE NOT EXISTS (
            SELECT 1 FROM sys.columns c
            WHERE c.object_id = childTable.object_id AND c.name = N'OrganizationId'
        )
        AND EXISTS (
            SELECT 1 FROM sys.columns c
            WHERE c.object_id = parentTable.object_id AND c.name = N'OrganizationId'
        );

    OPEN TenantlessChildren;
    FETCH NEXT FROM TenantlessChildren
        INTO @SchemaName, @TableName, @ChildColumn, @ParentSchema, @ParentTable, @ParentColumn;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @Sql = N'DELETE child FROM ' + QUOTENAME(@SchemaName) + N'.' + QUOTENAME(@TableName) + N' child ' +
            N'WHERE EXISTS (SELECT 1 FROM ' + QUOTENAME(@ParentSchema) + N'.' + QUOTENAME(@ParentTable) + N' parent ' +
            N'WHERE parent.' + QUOTENAME(@ParentColumn) + N' = child.' + QUOTENAME(@ChildColumn) +
            N' AND parent.OrganizationId <> @KeepOrganizationId); ' +
            N'SET @AffectedRows = @@ROWCOUNT;';
        EXEC sys.sp_executesql @Sql,
            N'@KeepOrganizationId UNIQUEIDENTIFIER, @AffectedRows INT OUTPUT',
            @KeepOrganizationId, @AffectedRows OUTPUT;
        SET @DeletedRows += @AffectedRows;

        FETCH NEXT FROM TenantlessChildren
            INTO @SchemaName, @TableName, @ChildColumn, @ParentSchema, @ParentTable, @ParentColumn;
    END
    CLOSE TenantlessChildren;
    DEALLOCATE TenantlessChildren;

    IF OBJECT_ID(N'shared.OutboxMessages', N'U') IS NOT NULL
    BEGIN
        DELETE FROM shared.OutboxMessages
        WHERE COALESCE(
            TRY_CONVERT(UNIQUEIDENTIFIER, JSON_VALUE(PayloadJson, '$.OrganizationId')),
            TRY_CONVERT(UNIQUEIDENTIFIER, JSON_VALUE(PayloadJson, '$.organizationId'))
        ) <> @KeepOrganizationId;
        SET @DeletedRows += @@ROWCOUNT;
    END

    DECLARE TenantTables CURSOR LOCAL FAST_FORWARD FOR
        SELECT s.name, t.name
        FROM sys.tables t
        JOIN sys.schemas s ON s.schema_id = t.schema_id
        WHERE EXISTS (
            SELECT 1 FROM sys.columns c
            WHERE c.object_id = t.object_id AND c.name = N'OrganizationId'
        );

    OPEN TenantTables;
    FETCH NEXT FROM TenantTables INTO @SchemaName, @TableName;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @Sql = N'DELETE FROM ' + QUOTENAME(@SchemaName) + N'.' + QUOTENAME(@TableName) +
            N' WHERE OrganizationId <> @KeepOrganizationId; SET @AffectedRows = @@ROWCOUNT;';
        EXEC sys.sp_executesql @Sql,
            N'@KeepOrganizationId UNIQUEIDENTIFIER, @AffectedRows INT OUTPUT',
            @KeepOrganizationId, @AffectedRows OUTPUT;
        SET @DeletedRows += @AffectedRows;
        FETCH NEXT FROM TenantTables INTO @SchemaName, @TableName;
    END
    CLOSE TenantTables;
    DEALLOCATE TenantTables;

    DELETE FROM core.Organizations WHERE Id <> @KeepOrganizationId;
    SET @DeletedRows += @@ROWCOUNT;

    DECLARE EnableConstraints CURSOR LOCAL FAST_FORWARD FOR
        SELECT s.name, t.name
        FROM sys.tables t
        JOIN sys.schemas s ON s.schema_id = t.schema_id;

    OPEN EnableConstraints;
    FETCH NEXT FROM EnableConstraints INTO @SchemaName, @TableName;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @Sql = N'ALTER TABLE ' + QUOTENAME(@SchemaName) + N'.' + QUOTENAME(@TableName) +
            N' WITH CHECK CHECK CONSTRAINT ALL;';
        EXEC sys.sp_executesql @Sql;
        FETCH NEXT FROM EnableConstraints INTO @SchemaName, @TableName;
    END
    CLOSE EnableConstraints;
    DEALLOCATE EnableConstraints;

    COMMIT TRANSACTION;

    SELECT @DeletedRows AS DeletedRows, Id, Code, Name
    FROM core.Organizations;
END TRY
BEGIN CATCH
    IF CURSOR_STATUS('local', 'DisableConstraints') >= 0 CLOSE DisableConstraints;
    IF CURSOR_STATUS('local', 'TenantlessChildren') >= 0 CLOSE TenantlessChildren;
    IF CURSOR_STATUS('local', 'TenantTables') >= 0 CLOSE TenantTables;
    IF CURSOR_STATUS('local', 'EnableConstraints') >= 0 CLOSE EnableConstraints;
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;