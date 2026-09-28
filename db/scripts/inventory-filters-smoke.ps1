$ErrorActionPreference = 'Stop'
$base = 'http://localhost:7072/api/v1'
function J($o) { $o | ConvertTo-Json -Depth 8 }

$login = (Invoke-RestMethod -Uri "$base/auth/login" -Method Post -ContentType 'application/json' -Body (J @{ organizationCode = 'DEMO'; email = 'demo@demo.local' })).data
$H = @{ Authorization = "Bearer $($login.accessToken)" }
$wh = (Invoke-RestMethod -Uri "$base/warehouses" -Headers $H).data
$item = (Invoke-RestMethod -Uri "$base/items?pageSize=1" -Headers $H).data[0]

# 1) Receive WITHOUT PO -> expect blocked
try {
  Invoke-RestMethod -Uri "$base/goods-receipts" -Method Post -Headers $H -ContentType 'application/json' -Body (J @{ warehouseId = $wh[0].id; lines = @(@{ itemId = $item.id; qty = 5; unitCost = 100 }) }) | Out-Null
  Write-Output 'NO_PO: unexpectedly succeeded'
} catch { Write-Output ('NO_PO blocked status=' + [int]$_.Exception.Response.StatusCode) }

# 2) Receive WITH PO -> expect ok
$grn = (Invoke-RestMethod -Uri "$base/goods-receipts" -Method Post -Headers $H -ContentType 'application/json' -Body (J @{ warehouseId = $wh[0].id; poReference = 'PO-TEST-01'; lines = @(@{ itemId = $item.id; qty = 5; unitCost = 100 }) })).data
Write-Output ('WITH_PO ok grn=' + $grn.number + ' ref=' + $grn.reference)

# 3) Transfer WITHOUT transport -> expect blocked
try {
  Invoke-RestMethod -Uri "$base/stock-transfers" -Method Post -Headers $H -ContentType 'application/json' -Body (J @{ fromWarehouseId = $wh[0].id; toWarehouseId = $wh[1].id; lines = @(@{ itemId = $item.id; qty = 1 }) }) | Out-Null
  Write-Output 'NO_TRANSPORT: unexpectedly succeeded'
} catch { Write-Output ('NO_TRANSPORT blocked status=' + [int]$_.Exception.Response.StatusCode) }

# 4) Transfer WITH transport -> expect ok
$trf = (Invoke-RestMethod -Uri "$base/stock-transfers" -Method Post -Headers $H -ContentType 'application/json' -Body (J @{ fromWarehouseId = $wh[0].id; toWarehouseId = $wh[1].id; transportId = 'MH12-AB-1234'; lines = @(@{ itemId = $item.id; qty = 1 }) })).data
Write-Output ('WITH_TRANSPORT ok trf=' + $trf.number + ' ref=' + $trf.reference)

# 5) Movements filter by type
$mv = Invoke-RestMethod -Uri "$base/stock/movements?movementType=RECEIPT&pageSize=5" -Headers $H
Write-Output ('MOVEMENTS filter RECEIPT total=' + $mv.pagination.totalItems)
Write-Output 'DONE'