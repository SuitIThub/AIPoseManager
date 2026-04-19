#requires -Version 5.1
<#
.SYNOPSIS
  Builds studio-pose-bridge.mcpb for Claude Desktop (MCP Bundle format).

.DESCRIPTION
  Zips manifest.json, pyproject.toml, README.md, and src/ into a .mcpb file (zip).
  Install: double-click the .mcpb in Claude Desktop, or use: npx @anthropic-ai/mcpb pack

.NOTES
  Legacy extension files used the .dxt extension; current standard is .mcpb (same idea).
  Spec: https://github.com/modelcontextprotocol/mcpb
#>
param(
    [string] $OutputPath = ""
)

$ErrorActionPreference = 'Stop'
$pkgRoot = $PSScriptRoot
$staging = Join-Path $pkgRoot 'dist\mcpb-staging'
$manifestSrc = Join-Path $pkgRoot 'claude-desktop\manifest.json'

if (-not (Test-Path -LiteralPath $manifestSrc)) {
    Write-Error "Missing $manifestSrc"
}

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $pkgRoot 'dist\studio-pose-bridge.mcpb'
}

Remove-Item -LiteralPath $staging -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $staging -Force | Out-Null

Copy-Item -LiteralPath $manifestSrc -Destination (Join-Path $staging 'manifest.json')
Copy-Item -LiteralPath (Join-Path $pkgRoot 'pyproject.toml') -Destination (Join-Path $staging 'pyproject.toml')
Copy-Item -LiteralPath (Join-Path $pkgRoot 'README.md') -Destination (Join-Path $staging 'README.md')
Copy-Item -LiteralPath (Join-Path $pkgRoot 'src') -Destination (Join-Path $staging 'src') -Recurse
Get-ChildItem -LiteralPath $staging -Recurse -Directory -Filter '__pycache__' -ErrorAction SilentlyContinue |
    Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
Get-ChildItem -LiteralPath $staging -Recurse -Directory -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -like '*.egg-info' } |
    Remove-Item -Recurse -Force -ErrorAction SilentlyContinue

$zipTemp = "$OutputPath.zip"
New-Item -ItemType Directory -Path (Split-Path -Parent $OutputPath) -Force | Out-Null
if (Test-Path -LiteralPath $zipTemp) { Remove-Item -LiteralPath $zipTemp -Force }
if (Test-Path -LiteralPath $OutputPath) { Remove-Item -LiteralPath $OutputPath -Force }

Compress-Archive -Path (Join-Path $staging '*') -DestinationPath $zipTemp -CompressionLevel Optimal
Remove-Item -LiteralPath $staging -Recurse -Force
Move-Item -LiteralPath $zipTemp -Destination $OutputPath

Write-Host "Created: $OutputPath" -ForegroundColor Green
Write-Host "Install: open this file with Claude Desktop, or Settings > Extensions > Install from file."
