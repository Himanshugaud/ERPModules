$ErrorActionPreference = "Stop"
$tok = az account get-access-token --resource https://database.windows.net/ --query accessToken -o tsv

$server = "erpmodulesqlserver.database.windows.net"
$db = "erpmodulessqldb"

# Show current project status + derived signals before touching anything.
$previewQuery = @"
SELECT
    p.Code, p.Name, ps.Code AS CurrentStatus,
    (SELECT COUNT(*) FROM inventory.MaterialRequirements mr WHERE mr.ProjectId = p.Id AND mr.Status IN ('SUBMITTED','APPROVED')) AS OpenRequirements,
    (SELECT COUNT(*) FROM inventory.StockTransfers st WHERE st.ProjectId = p.Id AND st.Status = 'DISPATCHED') AS InTransitTransfers,
    (SELECT COUNT(*) FROM inventory.StockTransfers st WHERE st.ProjectId = p.Id AND st.Status = 'RECEIVED') AS ReceivedTransfers
FROM project.Projects p
JOIN project.ProjectStatuses ps ON ps.Id = p.StatusId
WHERE ps.Code = 'ACTIVE'
ORDER BY p.Name;
"@
Write-Output "--- BEFORE ---"
Invoke-Sqlcmd -ServerInstance $server -Database $db -AccessToken $tok -Query $previewQuery | Format-Table -AutoSize

# One-time reconciliation: for projects currently sitting on the generic ACTIVE status, move them to the
# most advanced workflow phase implied by their actual requirement/shipment data (never regresses, never
# touches projects already on a specific phase status or on ON_HOLD/COMPLETED/CANCELLED).
$backfillQuery = @"
UPDATE p
SET p.StatusId = target.Id, p.UpdatedAt = SYSUTCDATETIME()
FROM project.Projects p
JOIN project.ProjectStatuses ps ON ps.Id = p.StatusId AND ps.Code = 'ACTIVE'
CROSS APPLY (
    SELECT TOP 1 target.Id, target.Code
    FROM project.ProjectStatuses target
    WHERE target.OrganizationId = p.OrganizationId
      AND (
          (target.Code = 'SHIPMENT_COMPLETED' AND EXISTS (SELECT 1 FROM inventory.StockTransfers st WHERE st.ProjectId = p.Id AND st.Status = 'RECEIVED'))
          OR (target.Code = 'SHIPMENT_IN_TRANSIT' AND NOT EXISTS (SELECT 1 FROM inventory.StockTransfers st WHERE st.ProjectId = p.Id AND st.Status = 'RECEIVED')
              AND EXISTS (SELECT 1 FROM inventory.StockTransfers st WHERE st.ProjectId = p.Id AND st.Status = 'DISPATCHED'))
          OR (target.Code = 'INVENTORY_CHECK' AND NOT EXISTS (SELECT 1 FROM inventory.StockTransfers st WHERE st.ProjectId = p.Id AND st.Status IN ('DISPATCHED','RECEIVED'))
              AND EXISTS (SELECT 1 FROM inventory.MaterialRequirements mr WHERE mr.ProjectId = p.Id AND mr.Status IN ('SUBMITTED','APPROVED')))
      )
    ORDER BY CASE target.Code WHEN 'SHIPMENT_COMPLETED' THEN 1 WHEN 'SHIPMENT_IN_TRANSIT' THEN 2 WHEN 'INVENTORY_CHECK' THEN 3 END
) AS target;
"@
Invoke-Sqlcmd -ServerInstance $server -Database $db -AccessToken $tok -Query $backfillQuery
Write-Output "Backfill applied."

Write-Output "--- AFTER ---"
$afterQuery = @"
SELECT p.Code, p.Name, ps.Code AS CurrentStatus
FROM project.Projects p
JOIN project.ProjectStatuses ps ON ps.Id = p.StatusId
ORDER BY p.Name;
"@
Invoke-Sqlcmd -ServerInstance $server -Database $db -AccessToken $tok -Query $afterQuery | Format-Table -AutoSize
