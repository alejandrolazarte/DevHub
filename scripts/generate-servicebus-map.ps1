<#
.SYNOPSIS
    Generates a Service Bus communication map by analyzing MassTransit integration events.

.DESCRIPTION
    Scans publisher projects for IntegrationEvent class/record definitions and subscriber
    projects for IntegrationEventHandler<T> consumers. Builds a publisher -> consumer -> event
    graph and injects it into the HTML template.

.PARAMETER ReposRoot
    Root directory containing all repositories.

.PARAMETER RepositoryNamePattern
    Regex used to filter candidate repository directories by name.

.PARAMETER DisplayNamePrefixToTrim
    Optional prefix removed from repository names when building display labels in the map.

.PARAMETER TemplateFile
    HTML template path.

.PARAMETER OutputFile
    Output HTML path.
#>
param(
    [string]$ReposRoot = $PSScriptRoot,
    [string]$RepositoryNamePattern = '.*',
    [string]$DisplayNamePrefixToTrim = '',
    [string]$TemplateFile = '',
    [string]$OutputFile = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not $TemplateFile) { $TemplateFile = Join-Path $ReposRoot 'docs/servicebus-map.template.html' }
if (-not $OutputFile) { $OutputFile = Join-Path $ReposRoot 'docs/servicebus-map.html' }

function Get-ShortName([string]$dirName, [string]$prefixToTrim) {
    if ($prefixToTrim -and $dirName.StartsWith($prefixToTrim, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $dirName.Substring($prefixToTrim.Length)
    }

    if ($dirName -match '\.(\w+)$') { return $Matches[1] }
    return $dirName
}

function Get-Role([string]$name) {
    return 'service'
}

function Add-Entry([hashtable]$map, [string]$key, [string]$value) {
    if (-not $map.ContainsKey($key)) {
        $map[$key] = [System.Collections.Generic.SortedSet[string]]::new()
    }
    [void]$map[$key].Add($value)
}

function Normalize([string]$raw) {
    return $raw.Trim() -replace 'IntegrationEvent$', ''
}

function ConvertTo-JsArray([string[]]$items, [scriptblock]$lineMapper) {
    if (-not $items) { return '[]' }
    $lines = $items | ForEach-Object { & $lineMapper $_ }
    return "[`n$(  $lines -join ",`n")`n  ]"
}

$rxPublisher = [regex]'(?:class|record)\s+([A-Za-z0-9_]+IntegrationEvent)\b'
$rxConsumer = [regex]'IntegrationEventHandler<\s*([A-Za-z0-9_]+IntegrationEvent)\s*>'
$rxLegacyPublish = [regex]'\bnew\s+([A-Za-z0-9_]+IntegrationEvent)\s*\('
$rxLegacyConsumer = [regex]'IIntegrationEventHandler<\s*([A-Za-z0-9_]+IntegrationEvent)\s*>'

Write-Host ""
Write-Host "  Service Bus Map Generator" -ForegroundColor Cyan
Write-Host "  ==========================" -ForegroundColor DarkGray
Write-Host ""
Write-Host "  Root       : $ReposRoot"
Write-Host "  Repo regex : $RepositoryNamePattern"
Write-Host "  Template   : $TemplateFile"
Write-Host "  Output     : $OutputFile"
Write-Host ""

if (-not (Test-Path $ReposRoot)) {
    Write-Error "Repository root not found: $ReposRoot"
    exit 1
}

$serviceDirs = Get-ChildItem $ReposRoot -Directory |
    Where-Object { $_.Name -match $RepositoryNamePattern } |
    Where-Object {
        (Test-Path (Join-Path $_.FullName 'src')) -or
        ($_.Name -eq 'Legacy')
    } |
    Sort-Object Name

if (-not $serviceDirs) {
    Write-Error "No repositories matched '$RepositoryNamePattern' in: $ReposRoot"
    exit 1
}

Write-Host "  Found $($serviceDirs.Count) candidate repos" -ForegroundColor DarkGray
Write-Host ""

$eventPublishers = @{}
$eventConsumers = @{}

foreach ($dir in $serviceDirs) {
    $name = Get-ShortName $dir.Name $DisplayNamePrefixToTrim
    $srcRoot = Join-Path $dir.FullName 'src'

    $pubCount = 0
    $conCount = 0

    if ($name -eq 'Legacy') {
        Get-ChildItem $dir.FullName -Recurse -Filter '*.cs' -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -notmatch '\\(bin|obj|packages)\\' -and $_.FullName -notmatch '\.Tests?\\' } |
            ForEach-Object {
                $text = Get-Content $_.FullName -Raw -ErrorAction SilentlyContinue
                if (-not $text) { return }

                if ($_.FullName -match '\\IntegrationEvents\\Services\\') {
                    foreach ($m in $rxLegacyPublish.Matches($text)) {
                        $evt = Normalize $m.Groups[1].Value
                        Add-Entry $eventPublishers $evt $name
                        $pubCount++
                    }
                }

                foreach ($m in $rxLegacyConsumer.Matches($text)) {
                    $evt = Normalize $m.Groups[1].Value
                    Add-Entry $eventConsumers $evt $name
                    $conCount++
                }
            }

        if ($pubCount -gt 0 -or $conCount -gt 0) {
            Write-Host ("  {0,-22} pub:{1,3}  con:{2,3}" -f $name, $pubCount, $conCount)
        }
        continue
    }

    if (-not (Test-Path $srcRoot)) { continue }

    $pubProjects = Get-ChildItem $srcRoot -Directory -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match '\.IE\.Publisher' }

    foreach ($proj in $pubProjects) {
        Get-ChildItem $proj.FullName -Recurse -Filter '*.cs' -ErrorAction SilentlyContinue |
            ForEach-Object {
                $text = Get-Content $_.FullName -Raw -ErrorAction SilentlyContinue
                if (-not $text) { return }
                foreach ($m in $rxPublisher.Matches($text)) {
                    $evt = Normalize $m.Groups[1].Value
                    Add-Entry $eventPublishers $evt $name
                    $pubCount++
                }
            }
    }

    $subProjects = Get-ChildItem $srcRoot -Directory -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match '\.IE\.Subscriber' }

    foreach ($proj in $subProjects) {
        Get-ChildItem $proj.FullName -Recurse -Filter '*.cs' -ErrorAction SilentlyContinue |
            ForEach-Object {
                $text = Get-Content $_.FullName -Raw -ErrorAction SilentlyContinue
                if (-not $text) { return }
                foreach ($m in $rxConsumer.Matches($text)) {
                    $evt = Normalize $m.Groups[1].Value
                    Add-Entry $eventConsumers $evt $name
                    $conCount++
                }
            }
    }

    if ($pubCount -gt 0 -or $conCount -gt 0) {
        Write-Host ("  {0,-22} pub:{1,3}  con:{2,3}" -f $name, $pubCount, $conCount)
    }
}

Write-Host ""

$edgeMap = @{}
$allEventNames = ($eventPublishers.Keys + $eventConsumers.Keys) | Sort-Object -Unique

foreach ($evt in $allEventNames) {
    $publishers = if ($eventPublishers.ContainsKey($evt)) { @($eventPublishers[$evt]) } else { @() }
    $consumers = if ($eventConsumers.ContainsKey($evt)) { @($eventConsumers[$evt]) } else { @() }

    foreach ($pub in $publishers) {
        foreach ($con in $consumers) {
            if ($pub -eq $con) { continue }
            Add-Entry $edgeMap "${pub}|${con}" $evt
        }
    }
}

$usedServices = [System.Collections.Generic.SortedSet[string]]::new()
foreach ($key in $edgeMap.Keys) {
    $parts = $key -split '\|'
    [void]$usedServices.Add($parts[0])
    [void]$usedServices.Add($parts[1])
}

$servicesJs = ConvertTo-JsArray @($usedServices) {
    param($svc)
    "    { id: '$svc', role: '$(Get-Role $svc)' }"
}

$edgesJs = ConvertTo-JsArray @($edgeMap.Keys | Sort-Object) {
    param($key)
    $parts = $key -split '\|'
    $evtsJs = ($edgeMap[$key] | ForEach-Object { "'$_'" }) -join ', '
    "    { pub: '$($parts[0])', con: '$($parts[1])', events: [$evtsJs] }"
}

$generatedAt = Get-Date -Format "yyyy-MM-dd HH:mm"

if (-not (Test-Path $TemplateFile)) {
    Write-Error "Template not found: $TemplateFile"
    exit 1
}

$template = Get-Content $TemplateFile -Raw -Encoding UTF8

$output = $template `
    -replace '__SERVICES_JSON__', $servicesJs `
    -replace '__RAW_EDGES_JSON__', $edgesJs `
    -replace '__GENERATED_AT__', $generatedAt

$outputDir = Split-Path $OutputFile
if (-not (Test-Path $outputDir)) { New-Item $outputDir -ItemType Directory | Out-Null }

Set-Content $OutputFile $output -Encoding UTF8 -NoNewline

Write-Host "  Services : $($usedServices.Count)" -ForegroundColor Green
Write-Host "  Edges    : $($edgeMap.Count)" -ForegroundColor Green
Write-Host "  Events   : $($allEventNames.Count)" -ForegroundColor Green
Write-Host ""
Write-Host "  Generated: $OutputFile" -ForegroundColor Cyan
Write-Host ""
