const assert = require('node:assert/strict');
const { randomUUID } = require('node:crypto');
const { readFileSync } = require('node:fs');
const { join } = require('node:path');
const { AzureCliCredential } = require('@azure/identity');
const sql = require('mssql');

const server = 'erpmodulesqlserver.database.windows.net';
const sourceDatabase = 'erpmodulessqldb';
const targetDatabase = 'erpmodulessqldb-staging';
const tables = [
    'core.Organizations', 'core.Departments', 'core.Roles', 'core.Permissions',
    'core.Users', 'core.UserRoles', 'core.RolePermissions',
    'inventory.UnitsOfMeasure', 'inventory.ItemCategories', 'inventory.Items'
];
const deferred = new Map([
    ['core.Departments', 'ManagerId'],
    ['inventory.ItemCategories', 'ParentCategoryId']
]);
const excluded = /^(PasswordHash|ExternalIdentityId|ProfileImageUrl|LogoUrl|LastLoginAt|CreatedBy|UpdatedBy)$/i;
const quote = (identifier) => `[${identifier.replaceAll(']', ']]')}]`;
const qualified = (name) => name.split('.').map(quote).join('.');
let stage = 'configuration';

function columnType(column) {
    switch (column.TypeName) {
        case 'uniqueidentifier': return sql.UniqueIdentifier;
        case 'nvarchar': return sql.NVarChar(column.MaxLength === -1 ? sql.MAX : column.MaxLength / 2);
        case 'varchar': return sql.VarChar(column.MaxLength === -1 ? sql.MAX : column.MaxLength);
        case 'nchar': return sql.NChar(column.MaxLength / 2);
        case 'char': return sql.Char(column.MaxLength);
        case 'bit': return sql.Bit;
        case 'int': return sql.Int;
        case 'smallint': return sql.SmallInt;
        case 'tinyint': return sql.TinyInt;
        case 'bigint': return sql.BigInt;
        case 'decimal': case 'numeric': return sql.Decimal(column.Precision, column.Scale);
        case 'datetime2': return sql.DateTime2(column.Scale);
        case 'datetime': return sql.DateTime;
        case 'date': return sql.Date;
        case 'float': return sql.Float;
        case 'money': return sql.Money;
        default: assert.fail(`Unsupported reference-data SQL type: ${column.TypeName}`);
    }
}

async function columns(connection, name) {
    const result = await new sql.Request(connection).input('table', sql.NVarChar(260), name).query(`
        SELECT c.name AS Name, ty.name AS TypeName, c.max_length AS MaxLength,
               c.precision AS Precision, c.scale AS Scale, c.is_nullable AS Nullable,
               c.default_object_id AS DefaultId
        FROM sys.columns c JOIN sys.types ty ON ty.user_type_id=c.user_type_id
        WHERE c.object_id=OBJECT_ID(@table) AND c.is_computed=0 AND c.is_identity=0
          AND ty.name NOT IN ('timestamp', 'rowversion') ORDER BY c.column_id;
    `);
    assert.ok(result.recordset.length, 'Required reference table is missing.');
    return result.recordset;
}

async function main() {
    assert.ok(process.argv.slice(2).every((argument) => ['--apply', '--inventory-opening', '--planning-catalog'].includes(argument)), 'Only --apply, --inventory-opening and --planning-catalog are accepted.');
    assert.ok(!(process.argv.includes('--inventory-opening') && process.argv.includes('--planning-catalog')), 'Choose only one import mode.');
    const apply = process.argv.includes('--apply');
    const settings = JSON.parse(readFileSync(join(__dirname, '../../local.settings.json'), 'utf8')).Values;
    assert.equal(settings.SQL_SERVER, server);
    assert.equal(settings.SQL_DATABASE, targetDatabase);
    assert.equal(settings.SQL_USER, 'erp_local_staging');
    const token = await new AzureCliCredential({ subscription: '69db84a9-2638-4ca3-b3cd-cdf9ee9796db' })
        .getToken('https://database.windows.net/.default');
    const common = { server, connectionTimeout: 20000, requestTimeout: 60000,
        options: { encrypt: true, trustServerCertificate: false, abortTransactionOnError: true } };
    const source = new sql.ConnectionPool({ ...common, database: sourceDatabase,
        authentication: { type: 'azure-active-directory-access-token', options: { token: token.token } } });
    const target = new sql.ConnectionPool({ ...common, database: targetDatabase,
        user: settings.SQL_USER, password: settings.SQL_PASSWORD });
    let sourceTransaction;
    let targetTransaction;
    try {
        stage = 'database identity checks';
        await source.connect();
        await target.connect();
        const sourceIdentity = await source.request().query('SELECT DB_NAME() AS Name, snapshot_isolation_state AS SnapshotState FROM sys.databases WHERE name=DB_NAME()');
        const targetIdentity = await target.request().query('SELECT DB_NAME() AS Name, USER_NAME() AS Principal');
        assert.equal(sourceIdentity.recordset[0].Name, sourceDatabase);
        assert.equal(sourceIdentity.recordset[0].SnapshotState, 1, 'Source snapshot isolation must already be enabled.');
        assert.equal(targetIdentity.recordset[0].Name, targetDatabase);
        assert.equal(targetIdentity.recordset[0].Principal, 'erp_local_staging');
        if (process.argv.includes('--planning-catalog')) {
            await copyPlanningCatalog(source, target, apply);
            return;
        }
        if (process.argv.includes('--inventory-opening')) {
            await copyOpeningInventory(source, target, apply);
            return;
        }
        stage = 'read-only source snapshot';
        sourceTransaction = new sql.Transaction(source);
        await sourceTransaction.begin(sql.ISOLATION_LEVEL.SNAPSHOT);
        const snapshots = new Map();
        for (const name of tables) {
            const sourceColumns = (await columns(sourceTransaction, name)).filter((column) => !excluded.test(column.Name));
            const targetColumns = await columns(target, name);
            const copyColumns = sourceColumns.filter((column) => targetColumns.some((targetColumn) => targetColumn.Name === column.Name));
            for (const column of targetColumns) {
                assert.ok(column.Nullable || column.DefaultId || copyColumns.some((copy) => copy.Name === column.Name), 'Required destination column cannot be populated.');
            }
            for (const column of copyColumns) {
                const destination = targetColumns.find((entry) => entry.Name === column.Name);
                assert.equal(column.TypeName, destination.TypeName, 'Source/destination SQL types differ.');
                columnType(destination);
            }
            const result = await new sql.Request(sourceTransaction).query(`SELECT ${copyColumns.map((column) => quote(column.Name)).join(',')} FROM ${qualified(name)}`);
            snapshots.set(name, { columns: copyColumns.map((column) => targetColumns.find((entry) => entry.Name === column.Name)), rows: result.recordset });
            console.log(`${name}: ${result.recordset.length} source records`);
        }
        await sourceTransaction.commit();
        sourceTransaction = null;
        if (!apply) {
            console.log('Preview only. --apply imports into empty staging reference tables; credentials and external identity links are excluded.');
            return;
        }
        stage = 'atomic staging import';
        targetTransaction = new sql.Transaction(target);
        await targetTransaction.begin(sql.ISOLATION_LEVEL.SERIALIZABLE);
        for (const name of tables.filter((entry) => entry !== 'core.Permissions')) {
            const count = await new sql.Request(targetTransaction).query(`SELECT COUNT_BIG(*) AS Total FROM ${qualified(name)} WITH (TABLOCKX, HOLDLOCK)`);
            assert.equal(Number(count.recordset[0].Total), 0, 'Destination reference tables must be empty; refusing to overwrite local data.');
        }
        const existing = await new sql.Request(targetTransaction).query('SELECT Id,Code FROM core.Permissions WITH (TABLOCKX,HOLDLOCK)');
        const permissionIds = new Map();
        const permissionSnapshot = snapshots.get('core.Permissions');
        permissionSnapshot.rows = permissionSnapshot.rows.filter((permission) => {
            const match = existing.recordset.find((entry) => entry.Code.toLowerCase() === permission.Code.toLowerCase());
            permissionIds.set(permission.Id.toLowerCase(), match ? match.Id : permission.Id);
            return !match;
        });
        for (const mapping of snapshots.get('core.RolePermissions').rows) {
            mapping.PermissionId = permissionIds.get(mapping.PermissionId.toLowerCase());
            assert.ok(mapping.PermissionId, 'Permission mapping is missing.');
        }
        for (const [name, snapshot] of snapshots) {
            if (!snapshot.rows.length) continue;
            stage = `atomic reference import: ${name}`;
            const bulk = new sql.Table(qualified(name));
            bulk.create = false;
            for (const column of snapshot.columns) bulk.columns.add(column.Name, columnType(column), { nullable: column.Nullable });
            for (const row of snapshot.rows) bulk.rows.add(...snapshot.columns.map((column) => column.Name === deferred.get(name) ? null : row[column.Name]));
            await new sql.Request(targetTransaction).bulk(bulk, { checkConstraints: true });
        }
        for (const [name, reference] of deferred) {
            for (const row of snapshots.get(name).rows.filter((entry) => entry[reference])) {
                await new sql.Request(targetTransaction).input('id', sql.UniqueIdentifier, row.Id)
                    .input('reference', sql.UniqueIdentifier, row[reference])
                    .query(`UPDATE ${qualified(name)} SET ${quote(reference)}=@reference WHERE Id=@id`);
            }
        }
        for (const [name, snapshot] of snapshots) {
            if (name === 'core.Permissions') continue;
            const count = await new sql.Request(targetTransaction).query(`SELECT COUNT_BIG(*) AS Total FROM ${qualified(name)}`);
            assert.equal(Number(count.recordset[0].Total), snapshot.rows.length, 'Imported record count mismatch.');
        }
        const secrets = await new sql.Request(targetTransaction).query('SELECT COUNT(*) AS Total FROM core.Users WHERE PasswordHash IS NOT NULL OR ExternalIdentityId IS NOT NULL');
        assert.equal(secrets.recordset[0].Total, 0, 'Production credentials/identity links must not be imported.');
        await targetTransaction.commit();
        targetTransaction = null;
        console.log('Committed staging reference data. All copied-table counts verified; production passwords/external identities excluded.');
    } finally {
        if (sourceTransaction) await sourceTransaction.rollback().catch(() => {});
        if (targetTransaction) await targetTransaction.rollback().catch(() => {});
        await source.close();
        await target.close();
    }
}

async function copyPlanningCatalog(source, target, apply) {
    const names = ['project.ProjectStatuses', 'project.ProjectPriorities', 'inventory.RequirementSheetTypes',
        'inventory.RequirementSheetSections', 'inventory.RequirementSheetSectionItems'];
    let snapshotTransaction;
    let importTransaction;
    try {
        stage = 'read-only planning reference snapshot';
        snapshotTransaction = new sql.Transaction(source);
        await snapshotTransaction.begin(sql.ISOLATION_LEVEL.SNAPSHOT);
        const snapshots = new Map();
        for (const name of names) {
            const available = (await columns(snapshotTransaction, name)).filter((column) => !excluded.test(column.Name));
            const destination = await columns(target, name);
            const selected = available.filter((column) => destination.some((entry) => entry.Name === column.Name));
            for (const column of destination) {
                assert.ok(column.Nullable || column.DefaultId || selected.some((entry) => entry.Name === column.Name), 'Missing required planning destination column.');
            }
            const copyColumns = selected.map((column) => {
                const targetColumn = destination.find((entry) => entry.Name === column.Name);
                assert.equal(column.TypeName, targetColumn.TypeName, 'Planning SQL type mismatch.');
                columnType(targetColumn);
                return targetColumn;
            });
            const result = await new sql.Request(snapshotTransaction).query(`SELECT ${selected.map((column) => quote(column.Name)).join(',')} FROM ${qualified(name)}`);
            snapshots.set(name, { columns: copyColumns, rows: result.recordset });
            console.log(`${name}: ${result.recordset.length} source records`);
        }
        await snapshotTransaction.commit();
        snapshotTransaction = null;
        if (!apply) {
            console.log('Preview only. --apply adds missing planning statuses/priorities and sheet definitions/mappings; existing local data is preserved.');
            return;
        }
        stage = 'atomic missing planning reference import';
        importTransaction = new sql.Transaction(target);
        await importTransaction.begin(sql.ISOLATION_LEVEL.SERIALIZABLE);
        for (const [name, snapshot] of snapshots) {
            const hasCode = snapshot.columns.some((column) => column.Name === 'Code');
            const existing = (await new sql.Request(importTransaction).query(`SELECT Id${hasCode ? ',OrganizationId,Code' : ''} FROM ${qualified(name)} WITH (TABLOCKX,HOLDLOCK)`)).recordset;
            const missing = snapshot.rows.filter((row) => {
                const match = existing.find((entry) => entry.Id.toLowerCase() === row.Id.toLowerCase());
                if (match) {
                    if (hasCode) assert.ok(match.Code === row.Code && match.OrganizationId === row.OrganizationId, 'Existing planning reference identity differs; refusing overwrite.');
                    return false;
                }
                if (hasCode) assert.ok(!existing.some((entry) => entry.OrganizationId === row.OrganizationId && entry.Code.toLowerCase() === row.Code.toLowerCase()), 'Planning reference code has a different local ID; refusing duplicate.');
                return true;
            });
            if (missing.length) {
                const bulk = new sql.Table(qualified(name));
                bulk.create = false;
                for (const column of snapshot.columns) bulk.columns.add(column.Name, columnType(column), { nullable: column.Nullable });
                for (const row of missing) bulk.rows.add(...snapshot.columns.map((column) => row[column.Name]));
                await new sql.Request(importTransaction).bulk(bulk, { checkConstraints: true });
            }
            const ids = new Set((await new sql.Request(importTransaction).query(`SELECT Id FROM ${qualified(name)}`)).recordset.map((entry) => entry.Id.toLowerCase()));
            assert.ok(snapshot.rows.every((row) => ids.has(row.Id.toLowerCase())), 'Planning reference verification failed.');
            console.log(`${name}: ${missing.length} missing records added; existing records preserved.`);
        }
        await importTransaction.commit();
        importTransaction = null;
        console.log('Committed missing planning catalogs only. Projects, requirements, stock, users and passwords unchanged.');
    } finally {
        if (snapshotTransaction) await snapshotTransaction.rollback().catch(() => {});
        if (importTransaction) await importTransaction.rollback().catch(() => {});
    }
}

async function copyOpeningInventory(source, target, apply) {
    const linkedProjects = 'SELECT ProjectId FROM inventory.Warehouses WHERE ProjectId IS NOT NULL';
    const copyPlan = [
        ['core.Clients', ''],
        ['project.ProjectStatuses', `WHERE Id IN (SELECT StatusId FROM project.Projects WHERE Id IN (${linkedProjects}))`],
        ['project.ProjectPriorities', `WHERE Id IN (SELECT PriorityId FROM project.Projects WHERE Id IN (${linkedProjects}))`],
        ['project.Projects', `WHERE Id IN (${linkedProjects})`],
        ['inventory.Warehouses', '']
    ];
    let snapshotTransaction;
    let importTransaction;
    try {
        stage = 'read-only client/warehouse/stock snapshot';
        snapshotTransaction = new sql.Transaction(source);
        await snapshotTransaction.begin(sql.ISOLATION_LEVEL.SNAPSHOT);
        const snapshots = new Map();
        for (const [name, condition] of copyPlan) {
            const available = (await columns(snapshotTransaction, name)).filter((column) => !excluded.test(column.Name));
            const destination = await columns(target, name);
            const selected = available.filter((column) => destination.some((entry) => entry.Name === column.Name));
            for (const column of destination) {
                assert.ok(column.Nullable || column.DefaultId || selected.some((entry) => entry.Name === column.Name), 'Missing required destination column.');
            }
            const copyColumns = selected.map((column) => {
                const targetColumn = destination.find((entry) => entry.Name === column.Name);
                assert.equal(column.TypeName, targetColumn.TypeName, 'Reference SQL type mismatch.');
                columnType(targetColumn);
                return targetColumn;
            });
            const result = await new sql.Request(snapshotTransaction).query(`SELECT ${selected.map((column) => quote(column.Name)).join(',')} FROM ${qualified(name)} ${condition}`);
            snapshots.set(name, { columns: copyColumns, rows: result.recordset });
            console.log(`${name}: ${result.recordset.length} source records`);
        }
        const stock = (await new sql.Request(snapshotTransaction).query(`
            SELECT Id,OrganizationId,ItemId,WarehouseId,BinId,BatchId,
                   CONVERT(varchar(64),QtyOnHand) AS QtyOnHand,
                   CONVERT(varchar(64),QtyReserved) AS QtyReserved,
                   CONVERT(varchar(64),QtyInTransit) AS QtyInTransit,
                   CONVERT(varchar(64),AvgUnitCost) AS AvgUnitCost
            FROM inventory.StockLevels;
        `)).recordset;
        for (const level of stock) {
            assert.ok(!level.BinId && !level.BatchId, 'Binned/batched stock needs a separately reviewed import.');
            assert.ok(!level.QtyOnHand.startsWith('-') && !level.AvgUnitCost.startsWith('-'), 'Negative stock/cost needs a separately reviewed import.');
            assert.equal(Number(level.QtyReserved), 0, 'Reserved stock cannot be reproduced by an opening adjustment.');
            assert.equal(Number(level.QtyInTransit), 0, 'In-transit stock cannot be reproduced by an opening adjustment.');
        }
        const nonzero = stock.filter((level) => Number(level.QtyOnHand) !== 0);
        const warehouseCount = new Set(nonzero.map((level) => level.WarehouseId)).size;
        console.log(`Stock snapshot: ${stock.length} levels; ${nonzero.length} nonzero lines; ${warehouseCount} warehouse-specific adjustment documents in ONE atomic import.`);
        await snapshotTransaction.commit();
        snapshotTransaction = null;
        if (!apply) {
            console.log('Preview only. Add --apply to import into empty staging client/warehouse/stock targets. Existing users/passwords/items are not modified.');
            return;
        }
        stage = 'atomic client/warehouse/opening-stock import';
        importTransaction = new sql.Transaction(target);
        await importTransaction.begin(sql.ISOLATION_LEVEL.SERIALIZABLE);
        for (const name of [...snapshots.keys(), 'inventory.StockLevels', 'inventory.StockMovements', 'inventory.StockAdjustments', 'inventory.StockAdjustmentLines']) {
            const count = await new sql.Request(importTransaction).query(`SELECT COUNT_BIG(*) AS Total FROM ${qualified(name)} WITH (TABLOCKX,HOLDLOCK)`);
            assert.equal(Number(count.recordset[0].Total), 0, 'Destination import tables must be empty; refusing to overwrite or duplicate local data.');
        }
        for (const [name, snapshot] of snapshots) {
            if (!snapshot.rows.length) continue;
            const bulk = new sql.Table(qualified(name));
            bulk.create = false;
            for (const column of snapshot.columns) bulk.columns.add(column.Name, columnType(column), { nullable: column.Nullable });
            for (const row of snapshot.rows) bulk.rows.add(...snapshot.columns.map((column) => row[column.Name]));
            await new sql.Request(importTransaction).bulk(bulk, { checkConstraints: true });
        }
        stage = 'creating session-scoped opening stock staging table';
        await new sql.Request(importTransaction).batch(`
            IF DB_NAME()<>N'erpmodulessqldb-staging' THROW 51000,'Non-staging target refused.',1;
            CREATE TABLE #OpeningStock (
                Id uniqueidentifier NOT NULL,OrganizationId uniqueidentifier NOT NULL,
                ItemId uniqueidentifier NOT NULL,WarehouseId uniqueidentifier NOT NULL,
                QtyOnHandText varchar(64) NOT NULL,QtyReservedText varchar(64) NOT NULL,
                QtyInTransitText varchar(64) NOT NULL,AvgUnitCostText varchar(64) NOT NULL
            );
        `);
        const stockBulk = new sql.Table('#OpeningStock');
        stockBulk.create = false;
        for (const name of ['Id', 'OrganizationId', 'ItemId', 'WarehouseId']) stockBulk.columns.add(name, sql.UniqueIdentifier, { nullable: false });
        for (const name of ['QtyOnHandText', 'QtyReservedText', 'QtyInTransitText', 'AvgUnitCostText']) stockBulk.columns.add(name, sql.VarChar(64), { nullable: false });
        for (const level of stock) stockBulk.rows.add(level.Id, level.OrganizationId, level.ItemId, level.WarehouseId, level.QtyOnHand, level.QtyReserved, level.QtyInTransit, level.AvgUnitCost);
        stage = 'bulk loading opening stock staging table';
        if (stock.length) await new sql.Request(importTransaction).bulk(stockBulk, { checkConstraints: true });
        const batchId = randomUUID();
        stage = 'posting and reconciling atomic opening adjustments';
        await new sql.Request(importTransaction).input('batchId', sql.UniqueIdentifier, batchId).query(`
            SET NOCOUNT ON;
            IF DB_NAME()<>N'erpmodulessqldb-staging' THROW 51000,'Non-staging target refused.',1;
            DECLARE @now datetime2=SYSUTCDATETIME();
            SELECT Id,OrganizationId,ItemId,WarehouseId,
                   CONVERT(decimal(19,4),QtyOnHandText) AS QtyOnHand,
                   CONVERT(decimal(19,4),QtyReservedText) AS QtyReserved,
                   CONVERT(decimal(19,4),QtyInTransitText) AS QtyInTransit,
                   CONVERT(decimal(19,4),AvgUnitCostText) AS AvgUnitCost
            INTO #Balances FROM #OpeningStock;
            IF EXISTS (
                SELECT 1 FROM #Balances b
                LEFT JOIN inventory.Items i ON i.Id=b.ItemId AND i.OrganizationId=b.OrganizationId
                LEFT JOIN inventory.Warehouses w ON w.Id=b.WarehouseId AND w.OrganizationId=b.OrganizationId
                WHERE i.Id IS NULL OR w.Id IS NULL
            ) THROW 51000,'Missing or cross-tenant item/warehouse dependency.',1;
            SELECT NEWID() AS Id,OrganizationId,WarehouseId,
                   CONVERT(nvarchar(50),N'ADJ-LOCAL-'+LEFT(CONVERT(nvarchar(36),@batchId),8)+N'-'+CONVERT(nvarchar(10),ROW_NUMBER() OVER(ORDER BY OrganizationId,WarehouseId))) AS AdjustmentNumber
            INTO #Adjustments FROM #Balances WHERE QtyOnHand<>0 GROUP BY OrganizationId,WarehouseId;
            INSERT inventory.StockAdjustments (Id,OrganizationId,AdjustmentNumber,WarehouseId,ReasonCode,AdjustmentDate,Status,Notes,CreatedAt)
                SELECT Id,OrganizationId,AdjustmentNumber,WarehouseId,N'OPENING_BALANCE',CONVERT(date,@now),N'POSTED',
                       N'Production stock snapshot for local development. Atomic import '+CONVERT(nvarchar(36),@batchId),@now FROM #Adjustments;
            SELECT NEWID() AS Id,b.OrganizationId,b.ItemId,b.WarehouseId,b.QtyOnHand,b.AvgUnitCost,a.Id AS AdjustmentId,i.BaseUomId
                INTO #Lines FROM #Balances b JOIN #Adjustments a ON a.OrganizationId=b.OrganizationId AND a.WarehouseId=b.WarehouseId
                JOIN inventory.Items i ON i.Id=b.ItemId WHERE b.QtyOnHand<>0;
            INSERT inventory.StockAdjustmentLines (Id,StockAdjustmentId,ItemId,QtyDelta,UomId,UnitCost)
                SELECT Id,AdjustmentId,ItemId,QtyOnHand,BaseUomId,AvgUnitCost FROM #Lines;
            INSERT inventory.StockMovements (Id,OrganizationId,ItemId,WarehouseId,MovementType,Direction,Qty,UnitCost,TotalCost,RefDocType,RefDocId,RefDocLineId,Remarks,OccurredAt)
                SELECT NEWID(),OrganizationId,ItemId,WarehouseId,N'ADJUSTMENT',N'IN',QtyOnHand,AvgUnitCost,
                       CONVERT(decimal(19,4),QtyOnHand*AvgUnitCost),N'ADJUSTMENT',AdjustmentId,Id,
                       N'Local opening stock; atomic import '+CONVERT(nvarchar(36),@batchId),@now FROM #Lines;
            INSERT inventory.StockLevels (Id,OrganizationId,ItemId,WarehouseId,QtyOnHand,QtyReserved,QtyInTransit,AvgUnitCost,UpdatedAt)
                SELECT NEWID(),OrganizationId,ItemId,WarehouseId,QtyOnHand,QtyReserved,QtyInTransit,AvgUnitCost,@now FROM #Balances;
            INSERT shared.AuditLogs (OrganizationId,EntityType,EntityId,Action,NewValuesJson,CorrelationId,CreatedAt)
                SELECT a.OrganizationId,N'STOCK_ADJUSTMENT',a.Id,N'CREATE',
                       (SELECT a.AdjustmentNumber,N'OPENING_BALANCE' AS ReasonCode,@batchId AS ImportBatchId FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),@batchId,@now
                FROM #Adjustments a;
            IF EXISTS (
                SELECT OrganizationId,ItemId,WarehouseId,QtyOnHand,QtyReserved,QtyInTransit,AvgUnitCost FROM #Balances
                EXCEPT SELECT OrganizationId,ItemId,WarehouseId,QtyOnHand,QtyReserved,QtyInTransit,AvgUnitCost FROM inventory.StockLevels
            ) OR EXISTS (
                SELECT OrganizationId,ItemId,WarehouseId,QtyOnHand,QtyReserved,QtyInTransit,AvgUnitCost FROM inventory.StockLevels
                EXCEPT SELECT OrganizationId,ItemId,WarehouseId,QtyOnHand,QtyReserved,QtyInTransit,AvgUnitCost FROM #Balances
            ) THROW 51000,'Stock balance/cost snapshot mismatch.',1;
            IF EXISTS (
                SELECT 1 FROM inventory.StockLevels s
                OUTER APPLY (SELECT COALESCE(SUM(CASE WHEN Direction=N'IN' THEN Qty ELSE -Qty END),0) AS Qty FROM inventory.StockMovements m WHERE m.ItemId=s.ItemId AND m.WarehouseId=s.WarehouseId AND m.OrganizationId=s.OrganizationId) ledger
                WHERE s.QtyOnHand<>ledger.Qty
            ) THROW 51000,'Opening ledger does not reconcile to stock.',1;
        `);
        for (const [name, snapshot] of snapshots) {
            const count = await new sql.Request(importTransaction).query(`SELECT COUNT_BIG(*) AS Total FROM ${qualified(name)}`);
            assert.equal(Number(count.recordset[0].Total), snapshot.rows.length, 'Reference copy count mismatch.');
        }
        const totals = (await new sql.Request(importTransaction).query(`
            SELECT (SELECT COUNT(*) FROM inventory.StockLevels) AS Levels,
                   (SELECT COUNT(*) FROM inventory.StockMovements) AS Movements,
                   (SELECT COUNT(*) FROM inventory.StockAdjustments) AS Adjustments,
                   (SELECT COUNT(*) FROM inventory.StockAdjustmentLines) AS Lines,
                   (SELECT COUNT(*) FROM inventory.StockMovements WHERE MovementType<>N'ADJUSTMENT' OR RefDocType<>N'ADJUSTMENT') AS WrongTypes;
        `)).recordset[0];
        assert.equal(totals.Levels, stock.length);
        assert.equal(totals.Movements, nonzero.length);
        assert.equal(totals.Lines, nonzero.length);
        assert.equal(totals.Adjustments, warehouseCount);
        assert.equal(totals.WrongTypes, 0);
        await importTransaction.commit();
        importTransaction = null;
        console.log(`Committed ONE staging transaction: ${totals.Levels} exact stock balances, ${totals.Lines} ADJUSTMENT lines, ${totals.Adjustments} warehouse-specific documents. Ledger reconciles; users/passwords/items unchanged.`);
    } finally {
        if (snapshotTransaction) await snapshotTransaction.rollback().catch(() => {});
        if (importTransaction) await importTransaction.rollback().catch(() => {});
    }
}

main().catch((error) => {
    console.error(`Reference-data copy failed during ${stage}; no credentials or row contents are logged.`);
    console.error(error instanceof assert.AssertionError ? error.message : `Error type: ${error.name}; SQL number: ${Number(error.number) || 'not available'}`);
    if (/^[A-Z_0-9]+$/.test(error.code || '')) console.error(`Driver code: ${error.code}`);
    process.exitCode = 1;
});