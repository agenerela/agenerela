<#
    Renders the wireframes in src/ to PNGs in png/.

    Needs any Chromium browser (Edge ships with Windows) and nothing else: no Unity,
    no Node, no packages. Each page sets its own pixel size in CSS; this script only
    opens it at that size and screenshots it at 2x, so the PNGs stay readable zoomed in.

        .\render.ps1                 # every figure
        .\render.ps1 00-sitemap      # just one

    Keep this file ASCII: Windows PowerShell 5.1 reads .ps1 as ANSI, and a stray dash
    or arrow in a string is enough to make it fail to parse.
#>
param([string[]]$Names)

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$src  = Join-Path $root 'src'
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

if (-not $Names) {
    $Names = Get-ChildItem $src -Filter *.html | ForEach-Object { $_.BaseName } | Sort-Object
}
New-Item -ItemType Directory -Force $out | Out-Null

# The sitemap sheet is wider than the six screen sheets. Keep these in step with the
# body{width;height} rule at the top of each HTML file, or the screenshot gets cropped.
$sizes = @{ '00-sitemap' = '1800,1125' }

foreach ($n in $Names) {
    $size = if ($sizes.ContainsKey($n)) { $sizes[$n] } else { '1600,1000' }
    $page = 'file:///' + (($src -replace '\\', '/') + "/$n.html")
    $png  = Join-Path $out "$n.png"

    $argv = @(
        '--headless=new', '--disable-gpu', '--hide-scrollbars', '--no-first-run',
        "--user-data-dir=$env:TEMP\agenerela-render",
        '--force-device-scale-factor=2',
        "--window-size=$size",
        "--screenshot=$png",
        $page
    )

    $p = Start-Process -FilePath $browser -ArgumentList $argv -Wait -PassThru -NoNewWindow
    '{0,-22} exit {1}   png/{0}.png' -f $n, $p.ExitCode
}
