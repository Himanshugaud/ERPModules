/*
  Planning workflow support:
  1) Links Stock Transfers back to the Material Requirement they fulfil (traceability
     between the Planning materials estimate and the Shipment dispatch).
  2) Grants Project Administrator / Project Contributor the ability to raise materials
     estimates (Material Requirements) from the Planning page — previously this was
     Inventory-tier only.

  Idempotent: safe to re-run.
*/
SET NOCOUNT ON;
BEGIN TRANSACTION;

IF COL_LENGTH(N'inventory.StockTransfers', N'MaterialRequirementId') IS NULL
    ALTER TABLE inventory.StockTransfers ADD MaterialRequirementId UNIQUEIDENTIFIER NULL;

DECLARE @orgId UNIQUEIDENTIFIER = (SELECT Id FROM core.Organizations WHERE Code = N'TYE');
IF @orgId IS NULL
BEGIN
    RAISERROR('TYE organization not found.', 16, 1);
    ROLLBACK TRANSACTION;
    RETURN;
END

DECLARE @Map TABLE (RoleName NVARCHAR(100), PermissionCode NVARCHAR(100));
INSERT INTO @Map (RoleName, PermissionCode) VALUES
(N'Project Administrator', N'materialrequirement.create'), (N'Project Administrator', N'materialrequirement.read'),
(N'Project Contributor', N'materialrequirement.create'), (N'Project Contributor', N'materialrequirement.read');

INSERT INTO core.RolePermissions (RoleId, PermissionId)
SELECT r.Id, p.Id
FROM @Map m
JOIN core.Roles r ON r.OrganizationId = @orgId AND r.Name = m.RoleName
JOIN core.Permissions p ON p.Code = m.PermissionCode
WHERE NOT EXISTS (SELECT 1 FROM core.RolePermissions ex WHERE ex.RoleId = r.Id AND ex.PermissionId = p.Id);

COMMIT TRANSACTION;

SELECT r.Name, r.Description, COUNT(rp.PermissionId) AS PermissionCount
FROM core.Roles r
LEFT JOIN core.RolePermissions rp ON rp.RoleId = r.Id
WHERE r.OrganizationId = @orgId
GROUP BY r.Name, r.Description
ORDER BY r.Name;
