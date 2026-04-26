# publish.ps1
# Publica una nueva versión de RepoOrchestra y reinicia el servicio Windows.
# Uso:
#   .\publish.ps1              → patch bump automático (1.0.0 → 1.0.1)
#   .\publish.ps1 -Version 1.2.0

param(
    [string]$Version = ""
)

# Auto-elevar si no somos admin (necesario para parar/arrancar el servicio Windows)
if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
        ).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Host "Solicitando elevación de permisos..." -ForegroundColor Yellow
    $args = "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`""
    if ($Version) { $args += " -Version $Version" }
    Start-Process powershell -Verb RunAs -ArgumentList $args -Wait
    exit
}

$ServiceName  = "RepoOrchestra"
$CsprojPath   = "src\Woffu.Tools.RepoOrchestra\Woffu.Tools.RepoOrchestra.csproj"
$PublishDir   = "publish"
$ProjectDir   = "src\Woffu.Tools.RepoOrchestra"

Set-Location $PSScriptRoot

# --- Leer versión actual del .csproj ---
[xml]$csproj = Get-Content $CsprojPath
$currentVersion = $csproj.Project.PropertyGroup.Version
if (-not $currentVersion) { $currentVersion = "1.0.0" }

# --- Calcular nueva versión ---
if ($Version -eq "") {
    $parts = $currentVersion.Split('.')
    $parts[2] = [int]$parts[2] + 1
    $Version = $parts -join '.'
}

Write-Host ""
Write-Host "🚀 Publicando RepoOrchestra v$Version (antes: v$currentVersion)" -ForegroundColor Cyan
Write-Host ""

# --- Bump versión en .csproj ---
$csprojContent = Get-Content $CsprojPath -Raw
$csprojContent = $csprojContent -replace "<Version>$currentVersion</Version>", "<Version>$Version</Version>"
$csprojContent = $csprojContent -replace "<AssemblyVersion>$currentVersion</AssemblyVersion>", "<AssemblyVersion>$Version</AssemblyVersion>"
Set-Content $CsprojPath $csprojContent
Write-Host "✏️  Versión actualizada a $Version en .csproj" -ForegroundColor Gray

# --- Parar servicio si está corriendo ---
$svc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($svc -and $svc.Status -eq 'Running') {
    Write-Host "⏹  Parando servicio..." -ForegroundColor Gray
    Stop-Service -Name $ServiceName -Force
    Start-Sleep -Seconds 2
}

# --- Publicar ---
Write-Host "📦 Publicando..." -ForegroundColor Gray
dotnet publish $ProjectDir `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -o $PublishDir `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -v quiet

if ($LASTEXITCODE -ne 0) {
    Write-Error "❌ Publish falló. Revisa los errores arriba."
    # Restaurar versión anterior
    $csprojContent = Get-Content $CsprojPath -Raw
    $csprojContent = $csprojContent -replace "<Version>$Version</Version>", "<Version>$currentVersion</Version>"
    $csprojContent = $csprojContent -replace "<AssemblyVersion>$Version</AssemblyVersion>", "<AssemblyVersion>$currentVersion</AssemblyVersion>"
    Set-Content $CsprojPath $csprojContent
    exit 1
}

Write-Host "✅ Publicado en .\$PublishDir\" -ForegroundColor Green

# --- Arrancar servicio ---
if ($svc) {
    Write-Host "▶️  Arrancando servicio..." -ForegroundColor Gray
    Start-Service -Name $ServiceName
    Write-Host ""
    Write-Host "✅ RepoOrchestra v$Version corriendo en http://localhost:5200" -ForegroundColor Green
    Write-Host "   El banner de actualización aparecerá al recargar el browser." -ForegroundColor Gray
} else {
    Write-Host ""
    Write-Host "⚠️  Servicio no instalado. Para instalarlo ejecuta (como Admin):" -ForegroundColor Yellow
    Write-Host "   .\install-service.ps1" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "   O para probarlo directamente:" -ForegroundColor Gray
    Write-Host "   .\$PublishDir\Woffu.Tools.RepoOrchestra.exe" -ForegroundColor Gray
}

Write-Host ""
