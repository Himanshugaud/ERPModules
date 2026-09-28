$ErrorActionPreference = 'Stop'
$base = 'https://erpmodulesbackend-f0bfefhhcehwa6cr.centralindia-01.azurewebsites.net/api/v1'
function J($o) { $o | ConvertTo-Json -Depth 8 }

$h = Invoke-RestMethod -Uri "$base/health"
Write-Output ('HEALTH ' + $h.status)

$login = (Invoke-RestMethod -Uri "$base/auth/login" -Method Post -ContentType 'application/json' -Body (J @{ organizationCode = 'DEMO'; email = 'demo@demo.local' })).data
$H = @{ Authorization = "Bearer $($login.accessToken)" }
Write-Output ('LOGIN ok perms=' + $login.user.permissions.Count)

$items = (Invoke-RestMethod -Uri "$base/items?pageSize=5" -Headers $H)
Write-Output ('ITEMS total=' + $items.pagination.totalItems)

$stock = (Invoke-RestMethod -Uri "$base/stock?pageSize=5" -Headers $H)
Write-Output ('STOCK lines=' + $stock.pagination.totalItems)

$wh = (Invoke-RestMethod -Uri "$base/warehouses" -Headers $H).data
$item = $items.data[0]
# PO-required rule live?
try {
  Invoke-RestMethod -Uri "$base/goods-receipts" -Method Post -Headers $H -ContentType 'application/json' -Body (J @{ warehouseId = $wh[0].id; lines = @(@{ itemId = $item.id; qty = 1; unitCost = 10 }) }) | Out-Null
  Write-Output 'PO_RULE: NOT enforced (unexpected)'
} catch { Write-Output ('PO_RULE enforced status=' + [int]$_.Exception.Response.StatusCode) }
Write-Output 'PROD_VERIFY_DONE'