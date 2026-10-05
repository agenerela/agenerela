<#
    Renders the presentation's figures in this folder to PNGs in png/.

    Same approach as docs/design/render.ps1: any Chromium browser, headless, no other
    tools. Each page sets its own CSS size, drawn at 133.3 CSS px per inch of the
    10 x 5.625 in slide (1200 px = the 9 in content width); screenshots are taken at 2.5x.

        .\render.ps1                 # every figure
        .\render.ps1 01-context      # just one

    Keep this file ASCII: Windows PowerShell 5.1 reads .ps1 as ANSI.
#>
param([string[]]$Names)

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$out  = Join-Path $root 'png'

$browser = @(
    "C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
    "C:\Program Files\Microsoft\Edge\Application\msedge.exe",
    "C:\Program Files\Google\Chrome\Application\chrome.exe",
    "C:\Program Files (x86)\Google\Chrome\Application\chrome.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $browser) {
    throw "No Edge or Chrome found. Any Chromium browser works: add its path to the list above."
}

# Keep in step with the body{width;height} rule at the top of each HTML file.
$sizes = @{
    '01-context'      = '1200,480'
    '02-agents'       = '1200,300'
    '03-actions'      = '1200,460'
    '04-availability' = '1200,460'
    '05-schema'       = '1200,460'
    '06-providers'    = '1200,460'
    '07-guards'       = '1200,460'
    '07-targets'      = '1200,460'
    '08-telemetry'    = '827,500'
}

if (-not $Names) {
    $Names = Get-ChildItem $root -Filter *.html | ForEach-Object { $_.BaseName } | Sort-Object
}
New-Item -ItemType Directory -Force $out | Out-Null

foreach ($n in $Names) {
    $size = $sizes[$n]
    if (-not $size) { throw "No size for $n. Add it to the sizes table." }
    $page = 'file:///' + (($root -replace '\\', '/') + "/$n.html")
    $png  = Join-Path $out "$n.png"

    $argv = @(
        '--headless=new', '--disable-gpu', '--hide-scrollbars', '--no-first-run',
        "--user-data-dir=$env:TEMP\agenerela-render",
        '--force-device-scale-factor=2.5',
        "--window-size=$size",
        "--screenshot=$png",
        $page
    )

    $p = Start-Process -FilePath $browser -ArgumentList $argv -Wait -PassThru -NoNewWindow
    '{0,-18} exit {1}   png/{0}.png' -f $n, $p.ExitCode
}
