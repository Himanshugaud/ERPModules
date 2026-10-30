const assert = require("node:assert/strict");
const { randomUUID } = require("node:crypto");
const { readFileSync } = require("node:fs");
const { join } = require("node:path");
const { before, after, test } = require("node:test");
const sql = require("mssql");

const values = JSON.parse(readFileSync(join(__dirname, "../../local.settings.json"), "utf8")).Values;
if (values.SQL_SERVER !== "erpmodulesqlserver.database.windows.net" ||
    values.SQL_DATABASE !== "erpmodulessqldb-staging" || values.SQL_USER !== "erp_local_staging" || !values.SQL_PASSWORD) {
    throw new Error("Refusing to test anything except the configured contained staging identity.");
}
const pool = new sql.ConnectionPool({
    server: values.SQL_SERVER,
    database: values.SQL_DATABASE,
    user: values.SQL_USER,
    password: values.SQL_PASSWORD,
    connectionTimeout: 20000,
    requestTimeout: 30000,
    options: { encrypt: true, trustServerCertificate: false, abortTransactionOnError: false }
});

before(async () => {
    await pool.connect();
    const result = await pool.request().query("SELECT DB_NAME() AS DatabaseName, USER_NAME() AS PrincipalName");
    assert.equal(result.recordset[0].DatabaseName, "erpmodulessqldb-staging");
    assert.equal(result.recordset[0].PrincipalName, "erp_local_staging");
});

after(async () => { await pool.close(); });

test("staging contains the actual ERP schema and permission definitions", async () => {
    const result = await pool.request().query(`
        SELECT (SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0) AS TableCount,
               (SELECT COUNT(*) FROM core.Permissions) AS PermissionCount,
               OBJECT_ID(N'core.usp_SeedOrganizationDefaults', N'P') AS SeedProcedure;
    `);
    assert.equal(result.recordset[0].TableCount, 69);
    assert.ok(result.recordset[0].PermissionCount > 0);
    assert.ok(result.recordset[0].SeedProcedure);
});

test("runtime has ERP data access without owner or schema-management privileges", async () => {
    const result = await pool.request().query(`
        SELECT IS_MEMBER('db_owner') AS IsOwner,
               HAS_PERMS_BY_NAME(DB_NAME(), 'DATABASE', 'CREATE TABLE') AS CanCreateTables,
               HAS_PERMS_BY_NAME(DB_NAME(), 'DATABASE', 'ALTER ANY USER') AS CanManageUsers,
               HAS_PERMS_BY_NAME('core.Organizations', 'OBJECT', 'SELECT') AS CanRead,
               HAS_PERMS_BY_NAME('core.Organizations', 'OBJECT', 'INSERT') AS CanInsert,
               HAS_PERMS_BY_NAME('core.Organizations', 'OBJECT', 'UPDATE') AS CanUpdate,
               HAS_PERMS_BY_NAME('core.Organizations', 'OBJECT', 'DELETE') AS CanDelete,
               HAS_PERMS_BY_NAME('core.usp_SeedOrganizationDefaults', 'OBJECT', 'EXECUTE') AS CanExecute;
    `);
    const permissions = result.recordset[0];
    for (const name of ["IsOwner", "CanCreateTables", "CanManageUsers"]) assert.equal(permissions[name], 0);
    for (const name of ["CanRead", "CanInsert", "CanUpdate", "CanDelete", "CanExecute"]) assert.equal(permissions[name], 1);
});

test("synthetic writes obey foreign keys and roll back without retained business data", async () => {
    const organizationId = randomUUID();
    const userId = randomUUID();
    const transaction = new sql.Transaction(pool);
    await transaction.begin();
    try {
        await new sql.Request(transaction)
            .input("organizationId", sql.UniqueIdentifier, organizationId)
            .input("userId", sql.UniqueIdentifier, userId)
            .input("code", sql.NVarChar(50), `STG-${organizationId}`)
            .query(`
                INSERT core.Organizations (Id, Code, Name) VALUES (@organizationId, @code, N'Synthetic rollback test');
                INSERT core.Users (Id, OrganizationId, Email) VALUES (@userId, @organizationId, N'rollback@example.test');
            `);
        const inside = await new sql.Request(transaction).input("userId", sql.UniqueIdentifier, userId)
            .query("SELECT COUNT(*) AS UserCount FROM core.Users WHERE Id=@userId");
        assert.equal(inside.recordset[0].UserCount, 1);
        await assert.rejects(
            new sql.Request(transaction).input("organizationId", sql.UniqueIdentifier, randomUUID())
                .query("INSERT core.Users (OrganizationId, Email) VALUES (@organizationId, N'foreign@example.test')"),
            (error) => error.number === 547
        );
    } finally {
        await transaction.rollback();
    }
    const remaining = await pool.request().input("organizationId", sql.UniqueIdentifier, organizationId)
        .query("SELECT COUNT(*) AS OrganizationCount FROM core.Organizations WHERE Id=@organizationId");
    assert.equal(remaining.recordset[0].OrganizationCount, 0);
});

test("concurrent status predicates allow exactly one receipt transition", async () => {
    const organizationId = randomUUID();
    const warehouseId = randomUUID();
    const receiptId = randomUUID();
    const bind = (request) => request
        .input("organizationId", sql.UniqueIdentifier, organizationId)
        .input("warehouseId", sql.UniqueIdentifier, warehouseId)
        .input("receiptId", sql.UniqueIdentifier, receiptId)
        .input("code", sql.NVarChar(50), `STG-${organizationId}`);
    const transaction = new sql.Transaction(pool);
    await transaction.begin();
    try {
        await bind(new sql.Request(transaction)).query(`
            INSERT core.Organizations (Id, Code, Name) VALUES (@organizationId, @code, N'Synthetic concurrency test');
            INSERT inventory.Warehouses (Id, OrganizationId, Code, Name, WarehouseType)
                VALUES (@warehouseId, @organizationId, N'TEST', N'Synthetic warehouse', N'MAIN_STORE');
            INSERT inventory.GoodsReceipts (Id, OrganizationId, GrnNumber, WarehouseId, Status)
                VALUES (@receiptId, @organizationId, @code, @warehouseId, N'PENDING_APPROVAL');
        `);
        await transaction.commit();
    } catch (error) {
        await transaction.rollback();
        throw error;
    }
    try {
        const outcomes = await Promise.all(["POSTED", "REJECTED"].map(async (status) => {
            const result = await pool.request().input("receiptId", sql.UniqueIdentifier, receiptId)
                .input("status", sql.NVarChar(30), status).query(`
                    UPDATE inventory.GoodsReceipts SET Status=@status WHERE Id=@receiptId AND Status=N'PENDING_APPROVAL';
                    SELECT @@ROWCOUNT AS Changed;
                `);
            return result.recordset[0].Changed;
        }));
        assert.deepEqual(outcomes.sort(), [0, 1]);
    } finally {
        await bind(pool.request()).query(`
            DELETE inventory.GoodsReceipts WHERE Id=@receiptId AND OrganizationId=@organizationId;
            DELETE inventory.Warehouses WHERE Id=@warehouseId AND OrganizationId=@organizationId;
            DELETE core.Organizations WHERE Id=@organizationId;
        `);
    }
    const remaining = await pool.request().input("organizationId", sql.UniqueIdentifier, organizationId)
        .query("SELECT COUNT(*) AS OrganizationCount FROM core.Organizations WHERE Id=@organizationId");
    assert.equal(remaining.recordset[0].OrganizationCount, 0);
});