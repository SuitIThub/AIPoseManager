#requires -Version 5.1
<#
.SYNOPSIS
  Builds StudioPoseBridge.dll (BepInEx plugin).

.PARAMETER Configuration
  Debug or Release (default: Release).

.PARAMETER Hs2StudioRoot
  Game root containing StudioNEOV2_Data and BepInEx. Overrides env HS2_STUDIO_ROOT.
  Default if unset: D:\Honey Select (same default as the .csproj).

.PARAMETER GameDir
  If set, copies the DLL to GameDir\BepInEx\plugins\StudioPoseBridge\ after build.
  Overrides env HS2_GAME_DIR.

.EXAMPLE
  .\build.ps1

.EXAMPLE
  .\build.ps1 -Hs2StudioRoot "E:\Games\Honey Select" -GameDir "E:\Games\Honey Select"
#>
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',

    [string] $Hs2StudioRoot = $env:HS2_STUDIO_ROOT,

    [string] $GameDir = $env:HS2_GAME_DIR
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($Hs2StudioRoot)) {
    $Hs2StudioRoot = 'D:\Honey Select'
}

$projectRoot = $PSScriptRoot
$csproj = Join-Path $projectRoot 'src\StudioPoseBridge\StudioPoseBridge.csproj'
if (-not (Test-Path -LiteralPath $csproj)) {
    Write-Error "Project not found: $csproj"
}

$repoRoot = (Resolve-Path (Join-Path $projectRoot '..')).Path
$outDll = Join-Path $repoRoot 'build\StudioPoseBridge.dll'

Write-Host "Building Studio Pose Bridge ($Configuration)..." -ForegroundColor Cyan
Write-Host "  HS2StudioRoot (MSBuild): $Hs2StudioRoot"

$msbuildArgs = @(
    'build',
    $csproj,
    '-c', $Configuration,
    '-v', 'minimal',
    "/p:HS2StudioRoot=$Hs2StudioRoot"
)

if (-not [string]::IsNullOrWhiteSpace($GameDir)) {
    Write-Host "  HS2_GAME_DIR (post-build copy): $GameDir"
    $msbuildArgs += "/p:HS2_GAME_DIR=$GameDir"
}

& dotnet @msbuildArgs
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

if (-not (Test-Path -LiteralPath $outDll)) {
    Write-Warning "Expected output not found: $outDll"
}
else {
    Write-Host "OK: $outDll" -ForegroundColor Green
}
