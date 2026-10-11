/*
  Workflow roles for the Requirement -> Planning -> Supply Chain -> Transport flow (TYE org).
    Supply Chain Team - approves/arranges material requests, raises and receives purchase orders, arranges transfers
    Transport Team    - dispatches approved transfers with vehicle details, confirms delivery
    Execution Team    - read access to published project plans, materials and tasks
  Also grants plan.manage to admin-level roles and document.* (attachments) to the working teams.
  Idempotent. Run after db/schema/workflow-schema.sql.
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
    (N'Supply Chain Team', N'Arranges inventory from warehouses, raises purchase orders and arranges transfers.'),
    (N'Transport Team',    N'Arranges vehicles, dispatches shipments to site and confirms delivery.'),
    (N'Execution Team',    N'Site execution: views published project plans, materials and tasks.');

INSERT INTO core.Roles (OrganizationId, Name, Description, IsSystemRole)
SELECT @orgId, r.Name, r.Description, 0
FROM @Roles r
WHERE NOT EXISTS (SELECT 1 FROM core.Roles ex WHERE ex.OrganizationId = @orgId AND ex.Name = r.Name);

DECLARE @Map TABLE (RoleName NVARCHAR(100), PermissionCode NVARCHAR(100));
INSERT INTO @Map (RoleName, PermissionCode) VALUES
(N'Supply Chain Team', N'project.read'), (N'Supply Chain Team', N'item.read'), (N'Supply Chain Team', N'warehouse.read'),
(N'Supply Chain Team', N'supplier.read'), (N'Supply Chain Team', N'stock.read'),
(N'Supply Chain Team', N'materialrequirement.read'), (N'Supply Chain Team', N'materialrequirement.approve'), (N'Supply Chain Team', N'materialrequirement.convert'),
(N'Supply Chain Team', N'purchaseorder.read'), (N'Supply Chain Team', N'purchaseorder.create'), (N'Supply Chain Team', N'purchaseorder.update'),
(N'Supply Chain Team', N'purchaseorder.approve'), (N'Supply Chain Team', N'purchaseorder.close'),
(N'Supply Chain Team', N'goodsreceipt.read'), (N'Supply Chain Team', N'goodsreceipt.create'),
(N'Supply Chain Team', N'transfer.read'), (N'Supply Chain Team', N'transfer.create'), (N'Supply Chain Team', N'transfer.approve'),
(N'Supply Chain Team', N'inventory.report.read'),
(N'Transport Team', N'project.read'), (N'Transport Team', N'warehouse.read'), (N'Transport Team', N'transfer.read'),
(N'Transport Team', N'transfer.dispatch'), (N'Transport Team', N'transfer.receive'),
(N'Execution Team', N'project.read'), (N'Execution Team', N'task.read'), (N'Execution Team', N'task.create'),
(N'Execution Team', N'task.update'), (N'Execution Team', N'materialrequirement.read'), (N'Execution Team', N'transfer.read'),
(N'Execution Team', N'stock.read'),
-- attachments (document.*) for every working team
(N'Supply Chain Team', N'document.read'), (N'Supply Chain Team', N'document.create'),
(N'Transport Team',    N'document.read'), (N'Transport Team',    N'document.create'),
(N'Execution Team',    N'document.read'), (N'Execution Team',    N'document.create'),
(N'Sales Team',        N'document.read'), (N'Sales Team',        N'document.create'),
(N'Planning Team',     N'document.read'), (N'Planning Team',     N'document.create'),
(N'Shipment Team',     N'document.read'), (N'Shipment Team',     N'document.create'),
-- planning team owns the calendar planner + publish
(N'Planning Team',     N'plan.manage');

INSERT INTO core.RolePermissions (RoleId, PermissionId)
SELECT r.Id, p.Id
FROM @Map m
JOIN core.Roles r ON r.OrganizationId = @orgId AND r.Name = m.RoleName
JOIN core.Permissions p ON p.Code = m.PermissionCode
WHERE NOT EXISTS (SELECT 1 FROM core.RolePermissions ex WHERE ex.RoleId = r.Id AND ex.PermissionId = p.Id);

-- Admin-level roles (all orgs) get plan.manage and document.* automatically
INSERT INTO core.RolePermissions (RoleId, PermissionId)
SELECT r.Id, p.Id
FROM core.Roles r
JOIN core.Permissions p ON p.Code IN (N'plan.manage', N'document.read', N'document.create', N'document.delete')
WHERE r.Name IN (N'SUPER_ADMIN', N'ADMIN', N'Administrator', N'Group Director', N'CEO')
  AND NOT EXISTS (SELECT 1 FROM core.RolePermissions ex WHERE ex.RoleId = r.Id AND ex.PermissionId = p.Id);

COMMIT TRANSACTION;

SELECT r.Name, COUNT(rp.PermissionId) AS PermissionCount
FROM core.Roles r
LEFT JOIN core.RolePermissions rp ON rp.RoleId = r.Id
WHERE r.OrganizationId = @orgId AND r.Name IN (N'Supply Chain Team', N'Transport Team', N'Execution Team', N'Planning Team', N'Sales Team', N'Shipment Team')
GROUP BY r.Name
ORDER BY r.Name;
