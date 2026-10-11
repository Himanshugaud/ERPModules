param(
  [Parameter(Mandatory)][string]$File
)
# Applies a GO-batched .sql file to Azure SQL using the az-login Entra token.
$ErrorActionPreference = 'Stop'
$tok = az account get-access-token --resource https://database.windows.net/ --query accessToken -o tsv
$text = Get-Content -Raw -Path $File
$batches = [regex]::Split($text, '(?im)^\s*GO\s*$') | Where-Object { $_.Trim() }
$i = 0
foreach ($b in $batches) {
  $i++
  Invoke-Sqlcmd -ServerInstance erpmodulesqlserver.database.windows.net -Database erpmodulessqldb -AccessToken $tok -Query $b -QueryTimeout 120 | Format-Table -AutoSize | Out-String | Write-Output
}
Write-Output "Applied $i batch(es) from $File"
