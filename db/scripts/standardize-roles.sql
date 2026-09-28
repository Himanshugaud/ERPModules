/*
  Standardizes TYE roles to a Viewer / Contributor / Administrator pattern per module,
  and removes duplicate full-access and duplicate mid-tier roles.

  Before:
    ADMIN, CEO, Execution Head, Group Director, Manager   -> all identical 68-permission full access
    HR Manager, Project Manager                           -> both identical 27-permission project access
    Inventory Manager, Inventory Viewer                    -> inventory full/read-only
    Project Viewer                                         -> project read-only
    Employee Manager, Purchase Executive                   -> specialized, kept as-is

  After:
    Administrator          (68 perms)  - single consolidated full-access role (renamed from ADMIN)
    Inventory Viewer        (read-only)
    Inventory Contributor   (new)       - day-to-day inventory ops, no delete/approve/close
    Inventory Administrator (renamed from Inventory Manager)
    Project Viewer          (read-only)
    Project Contributor     (new)       - day-to-day project ops, no delete/approve/reject
    Project Administrator   (renamed from Project Manager)
    Employee Manager                    - unchanged, specialized HR role
    Purchase Executive                  - unchanged, specialized procurement role

  Users on removed duplicate roles (CEO, Execution Head, Group Director, Manager) are
  reassigned to Administrator before their old role rows are deleted. HR Manager currently
  has no users assigned and is removed outright.

  Idempotent: safe to re-run.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @orgId UNIQUEIDENTIFIER = (SELECT Id FROM core.Organizations WHERE Code = N'TYE');
IF @orgId IS NULL THROW 50000, 'TYE organization not found.', 1;

UPDATE core.Roles SET Name = N'Administrator', Description = N'Full system access across all modules.'
WHERE OrganizationId = @orgId AND Name = N'ADMIN';

UPDATE core.Roles SET Name = N'Inventory Administrator'
WHERE OrganizationId = @orgId AND Name = N'Inventory Manager';

UPDATE core.Roles SET Name = N'Project Administrator'
WHERE OrganizationId = @orgId AND Name = N'Project Manager';

DECLARE @adminRoleId UNIQUEIDENTIFIER = (SELECT Id FROM core.Roles WHERE OrganizationId = @orgId AND Name = N'Administrator');

DECLARE @DupRoles TABLE (Id UNIQUEIDENTIFIER);
INSERT INTO @DupRoles (Id)
SELECT Id FROM core.Roles WHERE OrganizationId = @orgId AND Name IN (N'CEO', N'Execution Head', N'Group Director', N'Manager');

INSERT INTO core.UserRoles (UserId, RoleId)
SELECT DISTINCT ur.UserId, @adminRoleId
FROM core.UserRoles ur
JOIN @DupRoles d ON d.Id = ur.RoleId
WHERE NOT EXISTS (SELECT 1 FROM core.UserRoles ex WHERE ex.UserId = ur.UserId AND ex.RoleId = @adminRoleId);

DELETE ur FROM core.UserRoles ur JOIN @DupRoles d ON d.Id = ur.RoleId;
DELETE rp FROM core.RolePermissions rp JOIN @DupRoles d ON d.Id = rp.RoleId;
DELETE r FROM core.Roles r JOIN @DupRoles d ON d.Id = r.Id;

DECLARE @hrRoleId UNIQUEIDENTIFIER = (SELECT Id FROM core.Roles WHERE OrganizationId = @orgId AND Name = N'HR Manager');
IF @hrRoleId IS NOT NULL
BEGIN
    DELETE FROM core.UserRoles WHERE RoleId = @hrRoleId;
    DELETE FROM core.RolePermissions WHERE RoleId = @hrRoleId;
    DELETE FROM core.Roles WHERE Id = @hrRoleId;
END

DECLARE @Roles TABLE (Name NVARCHAR(100), Description NVARCHAR(500));
INSERT INTO @Roles (Name, Description) VALUES
    (N'Inventory Contributor', N'Day-to-day inventory, procurement and manufacturing operations; no delete or approval rights.'),
    (N'Project Contributor', N'Day-to-day project, task and timesheet operations; no delete or approval rights.');

INSERT INTO core.Roles (OrganizationId, Name, Description, IsSystemRole)
SELECT @orgId, r.Name, r.Description, 0
FROM @Roles r
WHERE NOT EXISTS (SELECT 1 FROM core.Roles ex WHERE ex.OrganizationId = @orgId AND ex.Name = r.Name);

DECLARE @Map TABLE (RoleName NVARCHAR(100), PermissionCode NVARCHAR(100));
INSERT INTO @Map (RoleName, PermissionCode) VALUES
(N'Inventory Contributor', N'item.create'), (N'Inventory Contributor', N'item.read'), (N'Inventory Contributor', N'item.update'),
(N'Inventory Contributor', N'warehouse.read'),
(N'Inventory Contributor', N'supplier.create'), (N'Inventory Contributor', N'supplier.read'), (N'Inventory Contributor', N'supplier.update'),
(N'Inventory Contributor', N'stock.read'),
(N'Inventory Contributor', N'goodsreceipt.create'), (N'Inventory Contributor', N'goodsreceipt.read'),
(N'Inventory Contributor', N'issue.create'), (N'Inventory Contributor', N'issue.read'),
(N'Inventory Contributor', N'transfer.create'), (N'Inventory Contributor', N'transfer.read'), (N'Inventory Contributor', N'transfer.dispatch'), (N'Inventory Contributor', N'transfer.receive'),
(N'Inventory Contributor', N'bom.create'), (N'Inventory Contributor', N'bom.read'), (N'Inventory Contributor', N'bom.update'),
(N'Inventory Contributor', N'workorder.create'), (N'Inventory Contributor', N'workorder.read'), (N'Inventory Contributor', N'workorder.complete'),
(N'Inventory Contributor', N'materialrequirement.create'), (N'Inventory Contributor', N'materialrequirement.read'),
(N'Inventory Contributor', N'purchaseorder.create'), (N'Inventory Contributor', N'purchaseorder.read'), (N'Inventory Contributor', N'purchaseorder.update'),
(N'Inventory Contributor', N'inventory.report.read'),
(N'Project Contributor', N'project.create'), (N'Project Contributor', N'project.read'), (N'Project Contributor', N'project.update'),
(N'Project Contributor', N'milestone.create'), (N'Project Contributor', N'milestone.read'), (N'Project Contributor', N'milestone.update'),
(N'Project Contributor', N'sprint.create'), (N'Project Contributor', N'sprint.read'), (N'Project Contributor', N'sprint.update'),
(N'Project Contributor', N'task.create'), (N'Project Contributor', N'task.read'), (N'Project Contributor', N'task.update'), (N'Project Contributor', N'task.assign'),
(N'Project Contributor', N'timesheet.create'), (N'Project Contributor', N'timesheet.read'), (N'Project Contributor', N'timesheet.update'),
(N'Project Contributor', N'document.create'), (N'Project Contributor', N'document.read'),
(N'Project Contributor', N'notification.read');

INSERT INTO core.RolePermissions (RoleId, PermissionId)
SELECT r.Id, p.Id
FROM @Map m
JOIN core.Roles r ON r.OrganizationId = @orgId AND r.Name = m.RoleName
JOIN core.Permissions p ON p.Code = m.PermissionCode
WHERE NOT EXISTS (SELECT 1 FROM core.RolePermissions ex WHERE ex.RoleId = r.Id AND ex.PermissionId = p.Id);

COMMIT TRANSACTION;

SELECT r.Name, r.Description, COUNT(rp.PermissionId) AS PermissionCount,
    STUFF((SELECT ', ' + u.Email FROM core.UserRoles ur JOIN core.Users u ON u.Id = ur.UserId WHERE ur.RoleId = r.Id FOR XML PATH('')), 1, 2, '') AS AssignedUsers
FROM core.Roles r
LEFT JOIN core.RolePermissions rp ON rp.RoleId = r.Id
WHERE r.OrganizationId = @orgId
GROUP BY r.Id, r.Name, r.Description
ORDER BY r.Name;
