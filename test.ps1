$org="11111111-1111-1111-1111-111111111111"
$user="22222222-2222-2222-2222-222222222222"
$status="A7BD61D3-5AA2-F111-9B33-000D3A36C0F0"
$priority="ACBD61D3-5AA2-F111-9B33-000D3A36C0F0"

Write-Host "=== TEST 1 ==="
curl.exe -s -i http://localhost:7072/api/v1/health

Write-Host "`n=== TEST 2 ==="
curl.exe -s -i "http://localhost:7072/api/v1/projects" -H "X-User-Id: $user" -H "X-Organization-Id: $org" -H "X-Permissions: project.read"

Write-Host "`n=== TEST 3 ==="
curl.exe -s -i -X POST "http://localhost:7072/api/v1/projects" -H "Content-Type: application/json" -H "X-User-Id: $user" -H "X-Organization-Id: $org" -H "X-Permissions: project.create" -d "{\`"code\`":\`"PRJ-001\`",\`"name\`":\`"Website Development\`",\`"statusId\`":\`"$status\`",\`"priorityId\`":\`"$priority\`",\`"managerId\`":\`"$user\`"}"

Write-Host "`n=== TEST 4 ==="
curl.exe -s -i "http://localhost:7072/api/v1/projects" -H "X-User-Id: $user" -H "X-Organization-Id: $org" -H "X-Permissions: project.read"

Write-Host "`n=== TEST 5 ==="
curl.exe -s -i "http://localhost:7072/api/v1/projects" -H "X-User-Id: $user" -H "X-Organization-Id: 99999999-9999-9999-9999-999999999999" -H "X-Permissions: project.read"

Write-Host "`n=== TEST 6 ==="
curl.exe -s -i -X POST "http://localhost:7072/api/v1/projects" -H "Content-Type: application/json" -H "X-User-Id: $user" -H "X-Organization-Id: $org" -H "X-Permissions: project.read" -d "{\`"code\`":\`"PRJ-002\`",\`"name\`":\`"X\`"}"

Write-Host "`n=== TEST 7 ==="
curl.exe -s -i -X POST "http://localhost:7072/api/v1/projects" -H "Content-Type: application/json" -H "X-User-Id: $user" -H "X-Organization-Id: $org" -H "X-Permissions: project.create" -d "{\`"code\`":\`"PRJ-001\`",\`"name\`":\`"Dup\`",\`"statusId\`":\`"$status\`",\`"priorityId\`":\`"$priority\`"}"

Write-Host "`n=== TEST 8 ==="
curl.exe -s -i -X POST "http://localhost:7072/api/v1/projects" -H "Content-Type: application/json" -H "X-User-Id: $user" -H "X-Organization-Id: $org" -H "X-Permissions: project.create" -d "{\`"code\`":\`"\`",\`"name\`":\`"\`"}"
