$baseUrl = $env:MASTER_URL
if (-not $baseUrl) { $baseUrl = "http://localhost:8080" }

Write-Host "SimCluster Task API Test" -ForegroundColor Cyan
Write-Host "Master URL: $baseUrl"

Write-Host "
Ping Master..."
Invoke-RestMethod "$baseUrl/api/master/ping"

Write-Host "
Workers:"
(Invoke-RestMethod "$baseUrl/api/master/workers").workers | Format-Table workerId, url, isReady, freeThreads

Write-Host "Submitting 10 tasks..."
$rng = [System.Random]::new()
for ($i = 1; $i -le 10; $i++) {
    $body = @{ name = "Test-$i"; durationMs = $rng.Next(1000, 5000); priority = $rng.Next(0, 3) } | ConvertTo-Json -Compress
    $null = Invoke-RestMethod -Method Post -Uri "$baseUrl/api/task" -ContentType "application/json" -Body $body
}
Write-Host "10 tasks submitted"

Write-Host "
Waiting for completion..."
Start-Sleep -Seconds 8

Write-Host "
Stats:"
Invoke-RestMethod "$baseUrl/api/task/stats"

Write-Host "
Distribution:"
(Invoke-RestMethod "$baseUrl/api/task").tasks | Group-Object assignedWorkerId | Select-Object Name, Count | Format-Table
