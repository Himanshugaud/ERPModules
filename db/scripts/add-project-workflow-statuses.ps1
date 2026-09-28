$ErrorActionPreference = "Stop"
$tok = az account get-access-token --resource https://database.windows.net/ --query accessToken -o tsv

$server = "erpmodulesqlserver.database.windows.net"
$db = "erpmodulessqldb"

# 1) Insert the 3 new workflow-phase statuses for every org that doesn't already have them (idempotent).
$insertQuery = @"
INSERT INTO project.ProjectStatuses (OrganizationId, Code, Name, DisplayOrder, IsDefault, IsFinal)
SELECT o.Id, v.Code, v.Name, v.DisplayOrder, 0, 0
FROM core.Organizations o
CROSS JOIN (VALUES
    (N'INVENTORY_CHECK',     N'Inventory Check',     2),
    (N'SHIPMENT_IN_TRANSIT', N'Shipment In Transit', 3),
    (N'SHIPMENT_COMPLETED',  N'Shipment Completed',  4)
) AS v(Code, Name, DisplayOrder)
WHERE NOT EXISTS (SELECT 1 FROM project.ProjectStatuses s WHERE s.OrganizationId = o.Id AND s.Code = v.Code);
"@
Invoke-Sqlcmd -ServerInstance $server -Database $db -AccessToken $tok -Query $insertQuery
Write-Output "Inserted new workflow statuses (where missing)."

# 2) Renumber DisplayOrder so the new phases sit between Planning and the generic Active/On Hold/Completed/Cancelled states.
$reorderQuery = @"
UPDATE project.ProjectStatuses SET DisplayOrder = 5 WHERE Code = 'ACTIVE';
UPDATE project.ProjectStatuses SET DisplayOrder = 6 WHERE Code = 'ON_HOLD';
UPDATE project.ProjectStatuses SET DisplayOrder = 7 WHERE Code = 'COMPLETED';
UPDATE project.ProjectStatuses SET DisplayOrder = 8 WHERE Code = 'CANCELLED';
"@
Invoke-Sqlcmd -ServerInstance $server -Database $db -AccessToken $tok -Query $reorderQuery
Write-Output "Renumbered DisplayOrder for existing statuses."

# 3) Show final status list per org for verification.
$verifyQuery = @"
SELECT o.Code AS OrgCode, s.Code, s.Name, s.DisplayOrder
FROM project.ProjectStatuses s
JOIN core.Organizations o ON o.Id = s.OrganizationId
ORDER BY o.Code, s.DisplayOrder;
"@
Invoke-Sqlcmd -ServerInstance $server -Database $db -AccessToken $tok -Query $verifyQuery | Format-Table -AutoSize
