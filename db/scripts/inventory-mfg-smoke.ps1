$ErrorActionPreference = 'Stop'
$base = 'http://localhost:7072/api/v1'
function J($o) { $o | ConvertTo-Json -Depth 8 }

$login = (Invoke-RestMethod -Uri "$base/auth/login" -Method Post -ContentType 'application/json' -Body (J @{ organizationCode = 'DEMO'; email = 'demo@demo.local' })).data
$H = @{ Authorization = "Bearer $($login.accessToken)" }
Write-Output ("LOGIN ok perms=" + $login.user.permissions.Count)

$wh = (Invoke-RestMethod -Uri "$base/warehouses" -Method Post -Headers $H -ContentType 'application/json' -Body (J @{ code = ('PRD' + (Get-Random -Max 9999)); name = 'Production Store'; warehouseType = 'PRODUCTION_STORE' })).data

$steel = (Invoke-RestMethod -Uri "$base/items" -Method Post -Headers $H -ContentType 'application/json' -Body (J @{ code = ('STL' + (Get-Random -Max 9999)); name = 'Steel Rod'; itemType = 'RAW_MATERIAL' })).data
$bolt = (Invoke-RestMethod -Uri "$base/items" -Method Post -Headers $H -ContentType 'application/json' -Body (J @{ code = ('BLT' + (Get-Random -Max 9999)); name = 'Bolt M12'; itemType = 'RAW_MATERIAL' })).data
$frame = (Invoke-RestMethod -Uri "$base/items" -Method Post -Headers $H -ContentType 'application/json' -Body (J @{ code = ('FRM' + (Get-Random -Max 9999)); name = 'Fabricated Frame'; itemType = 'FINISHED_GOOD'; isManufactured = $true })).data
Write-Output ("ITEMS steel=" + $steel.id.Substring(0,8) + " bolt=" + $bolt.id.Substring(0,8) + " frame=" + $frame.id.Substring(0,8))

# Receive raw materials: 100 steel @60, 300 bolts @2
Invoke-RestMethod -Uri "$base/goods-receipts" -Method Post -Headers $H -ContentType 'application/json' -Body (J @{ warehouseId = $wh.id; lines = @(
  @{ itemId = $steel.id; qty = 100; unitCost = 60 },
  @{ itemId = $bolt.id; qty = 300; unitCost = 2 }
) }) | Out-Null
Write-Output 'RAW received: steel 100@60, bolt 300@2'

# BOM: 1 Frame = 10 Steel + 20 Bolts
$bom = (Invoke-RestMethod -Uri "$base/boms" -Method Post -Headers $H -ContentType 'application/json' -Body (J @{ code = ('BOM' + (Get-Random -Max 9999)); outputItemId = $frame.id; outputQty = 1; lines = @(
  @{ componentItemId = $steel.id; qty = 10 },
  @{ componentItemId = $bolt.id; qty = 20 }
) })).data
Write-Output ("BOM " + $bom.code + " lines=" + $bom.lines.Count)

# Work order: produce 5 frames
$wo = (Invoke-RestMethod -Uri "$base/work-orders" -Method Post -Headers $H -ContentType 'application/json' -Body (J @{ outputItemId = $frame.id; bomId = $bom.id; plannedQty = 5; warehouseId = $wh.id })).data
Write-Output ("WO " + $wo.woNumber + " status=" + $wo.status + " components=" + $wo.components.Count + " plannedSteel=" + ($wo.components | Where-Object { $_.componentItemId -eq $steel.id }).plannedQty)

$wo = (Invoke-RestMethod -Uri "$base/work-orders/$($wo.id)/release" -Method Post -Headers $H).data
Write-Output ("WO released status=" + $wo.status)

$wo = (Invoke-RestMethod -Uri "$base/work-orders/$($wo.id)/complete" -Method Post -Headers $H -ContentType 'application/json' -Body (J @{ producedQty = 5; scrapQty = 0 })).data
Write-Output ("WO completed status=" + $wo.status + " produced=" + $wo.producedQty)

# Verify stock: steel 100-50=50, bolt 300-100=200, frame 5 @ (50*60+100*2)/5 = 640
$s = (Invoke-RestMethod -Uri "$base/stock?itemId=$($steel.id)" -Headers $H).data[0]
$b = (Invoke-RestMethod -Uri "$base/stock?itemId=$($bolt.id)" -Headers $H).data[0]
$f = (Invoke-RestMethod -Uri "$base/stock?itemId=$($frame.id)" -Headers $H).data[0]
Write-Output ("STEEL onHand=" + $s.qtyOnHand + " | BOLT onHand=" + $b.qtyOnHand + " | FRAME onHand=" + $f.qtyOnHand + " avgCost=" + $f.avgUnitCost)

# Try completing again (expect 409)
try {
  Invoke-RestMethod -Uri "$base/work-orders/$($wo.id)/complete" -Method Post -Headers $H -ContentType 'application/json' -Body (J @{ producedQty = 1 }) | Out-Null
  Write-Output 'RECOMPLETE unexpected success'
} catch { Write-Output ("RECOMPLETE blocked status=" + [int]$_.Exception.Response.StatusCode) }
Write-Output 'MFG_SMOKE_DONE'