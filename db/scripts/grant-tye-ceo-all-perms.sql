DECLARE @orgId UNIQUEIDENTIFIER = (SELECT Id FROM core.Organizations WHERE Code = N'TYE');
DECLARE @roleId UNIQUEIDENTIFIER = (SELECT Id FROM core.Roles WHERE OrganizationId = @orgId AND Name = N'CEO');
IF @orgId IS NULL THROW 50010, N'TYE org not found', 1;
IF @roleId IS NULL THROW 50011, N'CEO role not found', 1;

INSERT INTO core.RolePermissions (RoleId, PermissionId)
SELECT @roleId, p.Id
FROM core.Permissions p
WHERE NOT EXISTS (SELECT 1 FROM core.RolePermissions rp WHERE rp.RoleId = @roleId AND rp.PermissionId = p.Id);

SELECT (SELECT COUNT(*) FROM core.Permissions) AS TotalPermissions,
       (SELECT COUNT(*) FROM core.RolePermissions WHERE RoleId = @roleId) AS CeoPermissions;
