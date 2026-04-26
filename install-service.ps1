# install-service.ps1
# Registra RepoOrchestra como Windows Service (ejecutar como Administrador, una sola vez)

param(
    [string]$PublishPath = "C:\woffu-orchestra\publish\Woffu.Tools.RepoOrchestra.exe"
)

$ServiceName = "RepoOrchestra"
$DisplayName = "Repo Orchestra - Woffu Git Dashboard"
$Description = "Panel visual de estado git para los repositorios Woffu. UI en http://localhost:5200"

if (!(Test-Path $PublishPath)) {
    Write-Error "No encontrado el exe en $PublishPath. Ejecuta publish.ps1 primero."
    exit 1
}

$existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existing) {
    Write-Host "El servicio '$ServiceName' ya existe. Para reinstalar, ejecuta:" -ForegroundColor Yellow
    Write-Host "  sc.exe delete $ServiceName" -ForegroundColor Yellow
    exit 0
}

New-Service -Name $ServiceName `
            -BinaryPathName $PublishPath `
            -DisplayName $DisplayName `
            -Description $Description `
            -StartupType Automatic

Start-Service -Name $ServiceName

Write-Host ""
Write-Host "✅ Servicio '$ServiceName' instalado y arrancado." -ForegroundColor Green
Write-Host "   URL: http://localhost:5200" -ForegroundColor Cyan
Write-Host ""
Write-Host "Comandos útiles:" -ForegroundColor Gray
Write-Host "  Start-Service RepoOrchestra" -ForegroundColor Gray
Write-Host "  Stop-Service  RepoOrchestra" -ForegroundColor Gray
Write-Host "  Get-Service   RepoOrchestra" -ForegroundColor Gray
