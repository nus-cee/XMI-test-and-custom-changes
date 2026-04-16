param(
    [string]$Configuration = "Release",
    [string]$Solution = "RevitXmiExporter.sln",
    [switch]$SkipFormat
)

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path "$PSScriptRoot/../.."
Set-Location $repoRoot

function Invoke-Step {
    param(
        [string]$Label,
        [ScriptBlock]$Action
    )

    Write-Host "==> $Label" -ForegroundColor Cyan
    & $Action
    $exit = $LASTEXITCODE
    if ($exit -ne 0) {
        throw "Step '$Label' failed with exit code $exit"
    }
}

if (-not $SkipFormat) {
    Invoke-Step -Label "dotnet format" -Action {
        dotnet format --verify-no-changes
    }
} else {
    Write-Host "==> dotnet format skipped" -ForegroundColor Yellow
}

$msbuild = $Env:MSBUILD_EXE_PATH
if ([string]::IsNullOrWhiteSpace($msbuild)) {
    $command = Get-Command msbuild -ErrorAction SilentlyContinue
    if ($command) {
        $msbuild = $command.Source
    }
}

if (-not $msbuild) {
    throw "MSBuild not found. Launch a Visual Studio Developer PowerShell or set MSBUILD_EXE_PATH."
}

Invoke-Step -Label "msbuild $Solution" -Action {
    & $msbuild $Solution "/t:Restore;Build" "/p:Configuration=$Configuration" /m
}

Write-Host "All checks passed." -ForegroundColor Green
