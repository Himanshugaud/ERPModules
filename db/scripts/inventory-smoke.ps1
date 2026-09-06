$ErrorActionPreference = 'Stop'
$base = 'http://localhost:7072/api/v1'
function J($o) { $o | ConvertTo-Json -Depth 8 }

$login = (Invoke-RestMethod -Uri "$base/auth/login" -Method Post -ContentType 'application/json' -Body (J @{ organizationCode = 'DEMO'; email = 'demo@demo.local' })).data
$H = @{ Authorization = "Bearer $($login.accessToken)" }
Write-Output ("LOGIN ok user=" + $login.user.email + " perms=" + $login.user.permissions.Count)

$wh = Invoke-RestMethod -Uri "$base/warehouses" -Method Post -Headers $H -ContentType 'application/json' -Body (J @{ code = ('WH' + (Get-Random -Max 9999)); name = 'Main Plant Store'; warehouseType = 'MAIN_STORE' })
$site = Invoke-RestMethod -Uri "$base/warehouses" -Method Post -Headers $H -ContentType 'application/json' -Body (J @{ code = ('SITE' + (Get-Random -Max 9999)); name = 'Riverside Site Store'; warehouseType = 'SITE_STORE' })
Write-Output ("WAREHOUSE main=" + $wh.data.id + " site=" + $site.data.id)

$item = Invoke-RestMethod -Uri "$base/items" -Method Post -Headers $H -ContentType 'application/json' -Body (J @{ code = ('CEM' + (Get-Random -Max 9999)); name = 'OPC Cement 50kg'; itemType = 'RAW_MATERIAL'; reorderLevel = 100; trackBatches = $true })
Write-Output ("ITEM id=" + $item.data.id + " type=" + $item.data.itemType)

$grn = Invoke-RestMethod -Uri "$base/goods-receipts" -Method Post -Headers $H -ContentType 'application/json' -Body (J @{ warehouseId = $wh.data.id; lines = @(@{ itemId = $item.data.id; qty = 500; unitCost = 380; batchNo = 'B-2026-01'; expiryDate = '2027-01-01' }) })
Write-Output ("GRN " + $grn.data.number + " lines=" + $grn.data.lineCount + " value=" + $grn.data.totalValue)

# Second receipt at a different cost to verify weighted-average
$grn2 = Invoke-RestMethod -Uri "$base/goods-receipts" -Method Post -Headers $H -ContentType 'application/json' -Body (J @{ warehouseId = $wh.data.id; lines = @(@{ itemId = $item.data.id; qty = 500; unitCost = 420 }) })
Write-Output ("GRN2 " + $grn2.data.number + " value=" + $grn2.data.totalValue)

$stock = Invoke-RestMethod -Uri "$base/stock?itemId=$($item.data.id)" -Method Get -Headers $H
Write-Output ("STOCK onHand=" + $stock.data[0].qtyOnHand + " avgCost=" + $stock.data[0].avgUnitCost + " value=" + $stock.data[0].stockValue)

# Transfer 200 from main to site
$trf = Invoke-RestMethod -Uri "$base/stock-transfers" -Method Post -Headers $H -ContentType 'application/json' -Body (J @{ fromWarehouseId = $wh.data.id; toWarehouseId = $site.data.id; lines = @(@{ itemId = $item.data.id; qty = 200 }) })
Write-Output ("TRANSFER " + $trf.data.number + " value=" + $trf.data.totalValue)

# Issue 150 to a project from site store
$iss = Invoke-RestMethod -Uri "$base/material-issues" -Method Post -Headers $H -ContentType 'application/json' -Body (J @{ warehouseId = $site.data.id; issueType = 'PROJECT'; lines = @(@{ itemId = $item.data.id; qty = 150 }) })
Write-Output ("ISSUE " + $iss.data.number + " value=" + $iss.data.totalValue)

$main = Invoke-RestMethod -Uri "$base/stock?itemId=$($item.data.id)&warehouseId=$($wh.data.id)" -Method Get -Headers $H
$sst = Invoke-RestMethod -Uri "$base/stock?itemId=$($item.data.id)&warehouseId=$($site.data.id)" -Method Get -Headers $H
Write-Output ("MAIN onHand=" + $main.data[0].qtyOnHand + " | SITE onHand=" + $sst.data[0].qtyOnHand)

# Adjustment: write off 10 (damage) from site
$adj = Invoke-RestMethod -Uri "$base/stock-adjustments" -Method Post -Headers $H -ContentType 'application/json' -Body (J @{ warehouseId = $site.data.id; reasonCode = 'DAMAGE'; lines = @(@{ itemId = $item.data.id; qtyDelta = -10 }) })
Write-Output ("ADJUST " + $adj.data.number)

$mov = Invoke-RestMethod -Uri "$base/stock/movements?itemId=$($item.data.id)" -Method Get -Headers $H
Write-Output ("MOVEMENTS count=" + $mov.pagination.totalItems)
$mov.data | ForEach-Object { Write-Output ("  " + $_.movementType + " " + $_.direction + " qty=" + $_.qty + " cost=" + $_.unitCost) }

# Try to over-issue (expect 409)
try {
    Invoke-RestMethod -Uri "$base/material-issues" -Method Post -Headers $H -ContentType 'application/json' -Body (J @{ warehouseId = $site.data.id; lines = @(@{ itemId = $item.data.id; qty = 99999 }) }) | Out-Null
    Write-Output 'OVERISSUE unexpected success'
} catch {
    Write-Output ("OVERISSUE blocked status=" + [int]$_.Exception.Response.StatusCode)
}

$low = Invoke-RestMethod -Uri "$base/stock/low" -Method Get -Headers $H
Write-Output ("LOWSTOCK rows=" + $low.pagination.totalItems)
Write-Output 'SMOKE_DONE'