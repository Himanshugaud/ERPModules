/*
  Creates standard, module-scoped roles for the TYE organization:
    ADMIN             - complete access (all permissions)
    Inventory Manager - full read/write across inventory, procurement, manufacturing
    Inventory Viewer  - read-only across inventory, procurement, manufacturing
    Project Manager   - full read/write across projects, tasks, timesheets
    Project Viewer    - read-only across projects, tasks, timesheets
    Employee Manager  - manages employees/roles/passwords; reviews timesheets

  Idempotent: safe to re-run. Existing roles (CEO, Execution Head, Manager, HR Manager)
  and their assignments are left untouched.
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
    (N'ADMIN', N'Complete system access across all modules.'),
    (N'Inventory Manager', N'Full read/write access to inventory, procurement and manufacturing.'),
    (N'Inventory Viewer', N'Read-only access to inventory, procurement and manufacturing.'),
    (N'Project Manager', N'Full read/write access to projects, tasks and timesheets.'),
    (N'Project Viewer', N'Read-only access to projects and tasks.'),
    (N'Employee Manager', N'Manages employee records, role assignments and password resets.');

INSERT INTO core.Roles (OrganizationId, Name, Description, IsSystemRole)
SELECT @orgId, r.Name, r.Description, 0
FROM @Roles r
WHERE NOT EXISTS (SELECT 1 FROM core.Roles ex WHERE ex.OrganizationId = @orgId AND ex.Name = r.Name);

DECLARE @Map TABLE (RoleName NVARCHAR(100), PermissionCode NVARCHAR(100));
INSERT INTO @Map (RoleName, PermissionCode) VALUES
-- Inventory Manager (full inventory/procurement/manufacturing write access)
(N'Inventory Manager', N'item.create'), (N'Inventory Manager', N'item.read'), (N'Inventory Manager', N'item.update'), (N'Inventory Manager', N'item.delete'),
(N'Inventory Manager', N'warehouse.create'), (N'Inventory Manager', N'warehouse.read'), (N'Inventory Manager', N'warehouse.update'), (N'Inventory Manager', N'warehouse.delete'),
(N'Inventory Manager', N'supplier.create'), (N'Inventory Manager', N'supplier.read'), (N'Inventory Manager', N'supplier.update'), (N'Inventory Manager', N'supplier.delete'),
(N'Inventory Manager', N'stock.read'), (N'Inventory Manager', N'stock.adjust'),
(N'Inventory Manager', N'goodsreceipt.create'), (N'Inventory Manager', N'goodsreceipt.read'),
(N'Inventory Manager', N'issue.create'), (N'Inventory Manager', N'issue.read'),
(N'Inventory Manager', N'transfer.create'), (N'Inventory Manager', N'transfer.read'), (N'Inventory Manager', N'transfer.approve'), (N'Inventory Manager', N'transfer.dispatch'), (N'Inventory Manager', N'transfer.receive'),
(N'Inventory Manager', N'bom.create'), (N'Inventory Manager', N'bom.read'), (N'Inventory Manager', N'bom.update'), (N'Inventory Manager', N'bom.delete'),
(N'Inventory Manager', N'workorder.create'), (N'Inventory Manager', N'workorder.read'), (N'Inventory Manager', N'workorder.release'), (N'Inventory Manager', N'workorder.complete'),
(N'Inventory Manager', N'materialrequirement.create'), (N'Inventory Manager', N'materialrequirement.read'), (N'Inventory Manager', N'materialrequirement.approve'), (N'Inventory Manager', N'materialrequirement.convert'),
(N'Inventory Manager', N'purchaseorder.create'), (N'Inventory Manager', N'purchaseorder.read'), (N'Inventory Manager', N'purchaseorder.update'), (N'Inventory Manager', N'purchaseorder.approve'), (N'Inventory Manager', N'purchaseorder.close'),
(N'Inventory Manager', N'inventory.report.read'),
-- Inventory Viewer (read-only)
(N'Inventory Viewer', N'item.read'), (N'Inventory Viewer', N'warehouse.read'), (N'Inventory Viewer', N'supplier.read'), (N'Inventory Viewer', N'stock.read'),
(N'Inventory Viewer', N'goodsreceipt.read'), (N'Inventory Viewer', N'issue.read'), (N'Inventory Viewer', N'transfer.read'), (N'Inventory Viewer', N'bom.read'),
(N'Inventory Viewer', N'workorder.read'), (N'Inventory Viewer', N'materialrequirement.read'), (N'Inventory Viewer', N'purchaseorder.read'), (N'Inventory Viewer', N'inventory.report.read'),
-- Project Manager (full project/task/timesheet write access)
(N'Project Manager', N'project.create'), (N'Project Manager', N'project.read'), (N'Project Manager', N'project.update'), (N'Project Manager', N'project.delete'),
(N'Project Manager', N'milestone.create'), (N'Project Manager', N'milestone.read'), (N'Project Manager', N'milestone.update'), (N'Project Manager', N'milestone.delete'),
(N'Project Manager', N'sprint.create'), (N'Project Manager', N'sprint.read'), (N'Project Manager', N'sprint.update'), (N'Project Manager', N'sprint.delete'),
(N'Project Manager', N'task.create'), (N'Project Manager', N'task.read'), (N'Project Manager', N'task.update'), (N'Project Manager', N'task.delete'), (N'Project Manager', N'task.assign'),
(N'Project Manager', N'timesheet.create'), (N'Project Manager', N'timesheet.read'), (N'Project Manager', N'timesheet.update'), (N'Project Manager', N'timesheet.approve'), (N'Project Manager', N'timesheet.reject'),
(N'Project Manager', N'document.create'), (N'Project Manager', N'document.read'), (N'Project Manager', N'document.delete'),
(N'Project Manager', N'audit.read'), (N'Project Manager', N'notification.read'),
-- Project Viewer (read-only)
(N'Project Viewer', N'project.read'), (N'Project Viewer', N'milestone.read'), (N'Project Viewer', N'sprint.read'), (N'Project Viewer', N'task.read'),
(N'Project Viewer', N'timesheet.read'), (N'Project Viewer', N'document.read'), (N'Project Viewer', N'notification.read'),
-- Employee Manager (HR-focused; employee CRUD itself is role-name gated in the API)
(N'Employee Manager', N'project.read'), (N'Employee Manager', N'task.read'),
(N'Employee Manager', N'timesheet.read'), (N'Employee Manager', N'timesheet.approve'), (N'Employee Manager', N'timesheet.reject'),
(N'Employee Manager', N'document.read'), (N'Employee Manager', N'document.create'),
(N'Employee Manager', N'audit.read'), (N'Employee Manager', N'notification.read');

INSERT INTO core.RolePermissions (RoleId, PermissionId)
SELECT r.Id, p.Id
FROM @Map m
JOIN core.Roles r ON r.OrganizationId = @orgId AND r.Name = m.RoleName
JOIN core.Permissions p ON p.Code = m.PermissionCode
WHERE NOT EXISTS (SELECT 1 FROM core.RolePermissions ex WHERE ex.RoleId = r.Id AND ex.PermissionId = p.Id);

-- ADMIN gets every permission in the catalog
INSERT INTO core.RolePermissions (RoleId, PermissionId)
SELECT r.Id, p.Id
FROM core.Roles r
CROSS JOIN core.Permissions p
WHERE r.OrganizationId = @orgId AND r.Name = N'ADMIN'
  AND NOT EXISTS (SELECT 1 FROM core.RolePermissions ex WHERE ex.RoleId = r.Id AND ex.PermissionId = p.Id);

COMMIT TRANSACTION;

SELECT r.Name, r.Description, COUNT(rp.PermissionId) AS PermissionCount
FROM core.Roles r
LEFT JOIN core.RolePermissions rp ON rp.RoleId = r.Id
WHERE r.OrganizationId = @orgId
GROUP BY r.Name, r.Description
ORDER BY r.Name;
