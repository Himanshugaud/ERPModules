/*
  Adds two org-chart-specific roles for the TYE organization:
    Group Director     - senior leadership, full access (mirrors CEO-level permissions)
    Purchase Executive - front-line procurement role: can raise/track purchase requests
                         and goods receipts, but cannot approve, close, or adjust stock.

  Idempotent: safe to re-run. Existing roles are left untouched.
*/
SET NOCOUNT ON;
BEGIN TRANSACTION;

DECLARE @orgId UNIQUEIDENTIFIER = (SELECT Id FROM core.Organizations WHERE Code = 'TYE');
IF @orgId IS NULL
BEGIN
    RAISERROR('TYE organization not found.', 16, 1);
    ROLLBACK TRANSACTION;
    RETURN;
END

DECLARE @Roles TABLE (Name NVARCHAR(100), Description NVARCHAR(500));
INSERT INTO @Roles (Name, Description) VALUES
    (N'Group Director', N'Senior leadership with full oversight across all modules.'),
    (N'Purchase Executive', N'Raises and tracks purchase requests and goods receipts; no approval authority.');

INSERT INTO core.Roles (OrganizationId, Name, Description, IsSystemRole)
SELECT @orgId, r.Name, r.Description, 0
FROM @Roles r
WHERE NOT EXISTS (SELECT 1 FROM core.Roles ex WHERE ex.OrganizationId = @orgId AND ex.Name = r.Name);

DECLARE @Map TABLE (RoleName NVARCHAR(100), PermissionCode NVARCHAR(100));
INSERT INTO @Map (RoleName, PermissionCode) VALUES
(N'Purchase Executive', N'item.read'), (N'Purchase Executive', N'supplier.read'), (N'Purchase Executive', N'warehouse.read'),
(N'Purchase Executive', N'purchaseorder.create'), (N'Purchase Executive', N'purchaseorder.read'), (N'Purchase Executive', N'purchaseorder.update'),
(N'Purchase Executive', N'materialrequirement.create'), (N'Purchase Executive', N'materialrequirement.read'),
(N'Purchase Executive', N'goodsreceipt.create'), (N'Purchase Executive', N'goodsreceipt.read'),
(N'Purchase Executive', N'inventory.report.read');

INSERT INTO core.RolePermissions (RoleId, PermissionId)
SELECT r.Id, p.Id
FROM @Map m
JOIN core.Roles r ON r.OrganizationId = @orgId AND r.Name = m.RoleName
JOIN core.Permissions p ON p.Code = m.PermissionCode
WHERE NOT EXISTS (SELECT 1 FROM core.RolePermissions ex WHERE ex.RoleId = r.Id AND ex.PermissionId = p.Id);

-- Group Director gets every permission in the catalog (CEO-level access)
INSERT INTO core.RolePermissions (RoleId, PermissionId)
SELECT r.Id, p.Id
FROM core.Roles r
CROSS JOIN core.Permissions p
WHERE r.OrganizationId = @orgId AND r.Name = N'Group Director'
  AND NOT EXISTS (SELECT 1 FROM core.RolePermissions ex WHERE ex.RoleId = r.Id AND ex.PermissionId = p.Id);

COMMIT TRANSACTION;

SELECT r.Name, r.Description, COUNT(rp.PermissionId) AS PermissionCount
FROM core.Roles r
LEFT JOIN core.RolePermissions rp ON rp.RoleId = r.Id
WHERE r.OrganizationId = @orgId AND r.Name IN (N'Group Director', N'Purchase Executive')
GROUP BY r.Name, r.Description
ORDER BY r.Name;
