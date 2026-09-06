-- Creates a contained database admin user in erpmodulessqldb.
-- Run this while connected to the erpmodulessqldb database as the SQL server administrator.
-- Replace the placeholder values below before running.

DECLARE @message NVARCHAR(200);

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'erp_new_admin')
BEGIN
    CREATE USER [erp_new_admin] WITH PASSWORD = N'CHANGE_ME_Strong-Unique-Password!';
    ALTER ROLE db_owner ADD MEMBER [erp_new_admin];
    SET @message = N'User erp_new_admin created and added to db_owner.';
END
ELSE
BEGIN
    SET @message = N'User erp_new_admin already exists. No changes made.';
END

PRINT @message;
