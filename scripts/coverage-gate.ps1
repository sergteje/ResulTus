# Coverage gate (PowerShell): per-package >=90% lines, measured per module.
# Excluded deliberately: Analyzers and SourceGenerators (contract-driven coverage).
param(
    [string]$Root = (Split-Path $PSScriptRoot -Parent),
    [double]$Threshold = 0.90
)

$ErrorActionPreference = 'Stop'
$projects = @(
    @{ pkg = 'ResulTus';                 prefix = 'ResulTus/';                 tests = 'tests/ResulTus.Tests/ResulTus.Tests.csproj' },
    @{ pkg = 'ResulTus.Extensions';      prefix = 'ResulTus.Extensions/';      tests = 'tests/ResulTus.Extensions.Tests/ResulTus.Extensions.Tests.csproj' },
    @{ pkg = 'ResulTus.Collections';     prefix = 'ResulTus.Collections/';     tests = 'tests/ResulTus.Collections.Tests/ResulTus.Collections.Tests.csproj' },
    @{ pkg = 'ResulTus.Serialization';   prefix = 'ResulTus.Serialization/';   tests = 'tests/ResulTus.Serialization.Tests/ResulTus.Serialization.Tests.csproj' },
    @{ pkg = 'ResulTus.AspNetCore';      prefix = 'ResulTus.AspNetCore/';      tests = 'tests/ResulTus.AspNetCore.Tests/ResulTus.AspNetCore.Tests.csproj' },
    @{ pkg = 'ResulTus.HttpClient';      prefix = 'ResulTus.HttpClient/';      tests = 'tests/ResulTus.HttpClient.Tests/ResulTus.HttpClient.Tests.csproj' },
    @{ pkg = 'ResulTus.Logging';         prefix = 'ResulTus.Logging/';         tests = 'tests/ResulTus.Logging.Tests/ResulTus.Logging.Tests.csproj' },
    @{ pkg = 'ResulTus.Diagnostics';     prefix = 'ResulTus.Diagnostics/';     tests = 'tests/ResulTus.Diagnostics.Tests/ResulTus.Diagnostics.Tests.csproj' },
    @{ pkg = 'ResulTus.Validation';      prefix = 'ResulTus.Validation/';      tests = 'tests/ResulTus.Validation.Tests/ResulTus.Validation.Tests.csproj' },
    @{ pkg = 'ResulTus.Testing';         prefix = 'ResulTus.Testing/';         tests = 'tests/ResulTus.Testing.Tests/ResulTus.Testing.Tests.csproj' },
    @{ pkg = 'ResulTus.Validation.FluentValidation'; prefix = 'ResulTus.Validation.FluentValidation/'; tests = 'tests/ResulTus.Validation.Tests/ResulTus.Validation.Tests.csproj' }
)

$failures = 0
foreach ($entry in $projects) {
    $testProject = Join-Path $Root $entry.tests
    dotnet test $testProject -c Release --no-build --framework net10.0 --nologo --collect "XPlat Code Coverage" 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) { Write-Host "::error::$($entry.pkg): the own test project failed"; $failures++; continue }

    $file = Get-ChildItem (Join-Path $Root 'tests') -Recurse -Filter coverage.cobertura.xml |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1 -ExpandProperty FullName
    if (-not $file) { Write-Host "::error::$($entry.pkg): no cobertura produced"; $failures++; continue }

    $raw = [System.IO.File]::ReadAllText($file)
    $covered = 0.0; $valid = 0.0
    foreach ($classMatch in [regex]::Matches($raw, '<class .*?</class>', 'Singleline')) {
        $file2 = [regex]::Match($classMatch.Value, 'filename="([^"]+)"').Groups[1].Value
        if ($entry.prefix -eq 'ResulTus/') {
            if ($file2 -notmatch '^ResulTus/[A-Z]' -and $file2.Contains('/')) { continue }
            if ($file2 -match '^(ResulTus\.(?!Error\.cs))') { continue }
        } elseif (-not $file2.StartsWith($entry.prefix)) { continue }

        foreach ($lineMatch in [regex]::Matches($classMatch.Value, '<line number="\d+" hits="(\d+)')) {
            $valid++
            if ([int]$lineMatch.Groups[1].Value -gt 0) { $covered++ }
        }
    }

    if ($valid -eq 0) { Write-Host "::error::$($entry.pkg): no own-module lines traced"; $failures++; continue }

    $rate = $covered / $valid
    $display = ($rate * 100).ToString('0.00')
    if ($rate -ge $Threshold) {
        Write-Host "[gate] $($entry.pkg): $display% >= $([Math]::Round($Threshold * 100, 2))%  PASS"
    } else {
        Write-Host "::error::[gate] $($entry.pkg): $display% < $([Math]::Round($Threshold * 100, 2))% (own-module line threshold)"
        $failures++
    }
}

if ($failures -gt 0)
{
    Write-Host "::error::[gate] $failures package(s) below threshold or broken"
    exit 1
}
Write-Host "[gate] all packages at or above $([Math]::Round($Threshold * 100, 2))% lines"
