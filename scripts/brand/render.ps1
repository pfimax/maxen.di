<#
.SYNOPSIS
  Renders the README pictures (images/banner.png and images/three-places.png) from the HTML in this folder.

.DESCRIPTION
  Uses a headless Edge or Chrome, so there is nothing to install. PNG, not SVG, because nuget.org shows this README
  too and only renders PNG/JPG images from trusted hosts. The social preview image GitHub asks for in the repository
  settings is the banner, which is already 2x wide enough (1280x400 at 2x scale).

  Run from the repository root:  pwsh scripts/brand/render.ps1
#>
$ErrorActionPreference = "Stop"

$browser = @(
    "$env:ProgramFiles\Google\Chrome\Application\chrome.exe",
    "${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $browser) { throw "Neither Chrome nor Edge was found." }

function Render([string]$page, [string]$output, [int]$width, [int]$height) {
    $source = (Resolve-Path "scripts/brand/$page").Path
    $target = Join-Path (Get-Location) $output
    # The virtual time budget gives the web fonts time to load before the screenshot is taken.
    & $browser --headless=new --disable-gpu --hide-scrollbars --force-device-scale-factor=2 `
        --virtual-time-budget=8000 --window-size="$width,$height" --screenshot="$target" "file:///$($source -replace '\\','/')" | Out-Null
    Write-Host "wrote $output"
}

Render "banner.html" "images/banner.png" 1280 400
Render "three-places.html" "images/three-places.png" 1280 400
