# Prepares this folder on the RTX 5080 machine for the Agenerela ComfyUI stack.
# Run it from PowerShell in this folder:
#
#   powershell -ExecutionPolicy Bypass -File .\setup.ps1
#
# It checks Docker and the NVIDIA driver, lists the containers already on this
# machine (such as another ComfyUI) and makes sure this stack's names and ports do
# not clash with them, creates .env with a fresh random token, and creates the data
# folders. It only reads Docker's state: it starts, stops and changes no container,
# downloads nothing, and never overwrites an existing .env. Safe to re-run.
#
# Keep this file plain ASCII: Windows PowerShell 5.1 reads it in the ANSI code
# page, and some UTF-8 punctuation decodes to quote characters there.

Set-Location -LiteralPath $PSScriptRoot
$script:problems = 0
$project = 'agenerela-comfyui'
$ourContainers = 'agenerela-comfyui', 'agenerela-comfyui-gateway'

function Ok($message)   { Write-Host "  ok    $message" -ForegroundColor Green }
function Warn($message) { Write-Host "  warn  $message" -ForegroundColor Yellow }
function Fail($message) { Write-Host "  FAIL  $message" -ForegroundColor Red; $script:problems++ }

function Read-DotEnv($path) {
    $values = @{}
    foreach ($line in Get-Content -LiteralPath $path -Encoding UTF8) {
        if ($line -match '^\s*([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(.*?)\s*$') { $values[$Matches[1]] = $Matches[2] }
    }
    return $values
}

function Get-Listener($port) {
    $conn = Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $conn) { return $null }
    $process = Get-Process -Id $conn.OwningProcess -ErrorAction SilentlyContinue
    if ($process) { return $process.ProcessName }
    return "process $($conn.OwningProcess)"
}

Write-Host ""
Write-Host "Docker and GPU"

$dockerUp = $false
if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    Fail "docker was not found. Install Docker Desktop first (README, step 2)."
} else {
    docker info --format '{{.ServerVersion}}' *> $null
    if ($LASTEXITCODE -ne 0) { Fail "Docker is installed but not running. Start Docker Desktop and wait for 'Engine running'." }
    else { Ok "Docker is running"; $dockerUp = $true }
    docker compose version *> $null
    if ($LASTEXITCODE -ne 0) { Fail "'docker compose' is missing. Update Docker Desktop." }
}

if (-not (Get-Command nvidia-smi -ErrorAction SilentlyContinue)) {
    Fail "nvidia-smi was not found. Install the NVIDIA driver."
} else {
    $smi = (nvidia-smi 2>&1 | Out-String).Trim()
    if ($LASTEXITCODE -ne 0) {
        Warn "nvidia-smi failed: $(($smi -split "`n")[0].Trim().TrimEnd('.')). Check the driver (README, step 1)."
    } elseif ($smi -match 'CUDA (?:UMD )?Version:\s*(\d+)\.(\d+)') {  # newer drivers print "CUDA UMD Version"
        $cuda = [version]"$($Matches[1]).$($Matches[2])"
        if ($cuda -lt [version]'13.0') { Fail "The driver supports CUDA $cuda; the image needs 13.0 or newer. Update the NVIDIA driver." }
        else { Ok "NVIDIA driver supports CUDA $cuda" }
        $gpu = (nvidia-smi --query-gpu=name,memory.total --format=csv,noheader) -join '; '
        Ok "GPU: $gpu"
    } else {
        Warn "Could not read the CUDA version from nvidia-smi."
    }
}

# Every container on this machine, so names and ports can be checked against them.
$containers = @()
if ($dockerUp) {
    foreach ($line in (docker ps -a --format '{{.Names}}|{{.Image}}|{{.State}}|{{.Ports}}|{{.Labels}}')) {
        $f = $line -split '\|', 5
        if ($f.Count -lt 5) { continue }
        $owner = if ($f[4] -match 'com\.docker\.compose\.project=([^,]*)') { $Matches[1] } else { '' }
        $containers += [pscustomobject]@{ Name = $f[0]; Image = $f[1]; State = $f[2]; Ports = $f[3]; Project = $owner }
    }

    Write-Host ""
    Write-Host "Containers already on this machine (this script and this stack leave them alone)"
    $others = @($containers | Where-Object { $_.Project -ne $project })
    $squatters = @($others | Where-Object { $ourContainers -contains $_.Name })
    $comfyOthers = @($others | Where-Object { ($_.Name -match 'comfy' -or $_.Image -match 'comfy') -and $ourContainers -notcontains $_.Name })
    foreach ($c in $comfyOthers) {
        $ports = if ($c.Ports) { ", ports $($c.Ports)" } else { '' }
        Ok "Your other ComfyUI: container '$($c.Name)' ($($c.Image), $($c.State)$ports)"
    }
    $rest = $others.Count - $comfyOthers.Count - $squatters.Count
    if ($rest -gt 0 -and $comfyOthers.Count -gt 0) { Ok "plus $rest unrelated container(s)" }
    elseif ($rest -gt 0) { Ok "$rest container(s), none of them ComfyUI" }
    if ($others.Count -eq 0) { Ok "None" }
    foreach ($c in $squatters) {
        Fail "A container named '$($c.Name)' already exists and does not belong to this stack. Rename or remove it yourself if it is yours; this stack will not."
    }
    $ours = @($containers | Where-Object { $_.Project -eq $project })
    if ($ours.Count -gt 0) { Ok "This stack already has: $(($ours | ForEach-Object { "$($_.Name) ($($_.State))" }) -join ', ')" }
}

Write-Host ""
Write-Host "Settings"

$envFile = Join-Path $PSScriptRoot '.env'
if (Test-Path -LiteralPath $envFile) {
    Ok ".env already exists; left unchanged"
} else {
    $bytes = New-Object byte[] 32
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    $rng.GetBytes($bytes)
    $rng.Dispose()
    $token = -join ($bytes | ForEach-Object { $_.ToString('x2') })
    $template = Get-Content -LiteralPath (Join-Path $PSScriptRoot '.env.example') -Raw -Encoding UTF8
    $content = $template -replace '(?m)^COMFY_API_TOKEN=[^\r\n]*', "COMFY_API_TOKEN=$token"
    [System.IO.File]::WriteAllText($envFile, $content, (New-Object System.Text.UTF8Encoding $false))
    Ok "Created .env with a new random COMFY_API_TOKEN"
}

$settings = Read-DotEnv $envFile
if ($settings['COMFY_API_TOKEN'].Length -lt 32) { Fail "COMFY_API_TOKEN in .env is shorter than 32 characters. Delete .env and run this again." }
$uiPort  = if ($settings['COMFY_UI_PORT'])  { [int]$settings['COMFY_UI_PORT'] }  else { 8288 }
$apiPort = if ($settings['COMFY_API_PORT']) { [int]$settings['COMFY_API_PORT'] } else { 8289 }
$dataDir = if ($settings['COMFY_DATA_DIR']) { $settings['COMFY_DATA_DIR'] } else { './data' }

foreach ($sub in 'input', 'output', 'user') {
    New-Item -ItemType Directory -Force -Path (Join-Path $dataDir $sub) | Out-Null
}
Ok "Data folders ready under $dataDir"

Write-Host ""
Write-Host "Ports"

foreach ($port in $uiPort, $apiPort) {
    $publisher = $containers | Where-Object { $_.Ports -match ":$port->" } | Select-Object -First 1
    $listener = Get-Listener $port
    if ($publisher -and $publisher.Project -eq $project) {
        Ok "Port $port is this stack's own ('$($publisher.Name)' is running)"
    } elseif ($publisher) {
        Fail "Port $port is taken by container '$($publisher.Name)', which is not part of this stack. Pick a free port for COMFY_UI_PORT or COMFY_API_PORT in .env; do not change that container."
    } elseif ($listener) {
        Fail "Port $port is already used by $listener. Pick a free port for COMFY_UI_PORT or COMFY_API_PORT in .env."
    } else {
        Ok "Port $port is free"
    }
}
foreach ($port in 8188, 8000) {
    $publisher = $containers | Where-Object { $_.Ports -match ":$port->" } | Select-Object -First 1
    if ($publisher) { Ok "Port $port belongs to container '$($publisher.Name)'. This stack does not use it." }
    elseif (Get-Listener $port) { Ok "Port $port is in use, likely by your other ComfyUI. This stack does not use it." }
}

Write-Host ""
Write-Host "Addresses other machines can use for COMFY_URL"
$addresses = Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue |
    Where-Object { $_.IPAddress -notlike '127.*' -and $_.IPAddress -notlike '169.254.*' -and $_.InterfaceAlias -notmatch 'vEthernet|WSL|Loopback' }
foreach ($address in $addresses) {
    Write-Host ("  http://{0}:{1}   ({2})" -f $address.IPAddress, $apiPort, $address.InterfaceAlias)
}

Write-Host ""
if ($script:problems -gt 0) {
    Write-Host "$($script:problems) problem(s) above. Fix them, then run this again." -ForegroundColor Red
    exit 1
}
Write-Host "Ready. Next, from this folder (README, steps 6 to 9):" -ForegroundColor Green
Write-Host "  docker compose build"
Write-Host "  docker compose run --rm comfyui python -c `"import torch; print(torch.cuda.get_device_name(0))`""
Write-Host "  docker compose run --rm comfyui python /opt/agenerela/download_models.py"
Write-Host "  docker compose up -d"
Write-Host "Then copy the COMFY_API_TOKEN line from .env into tools\comfyui\.env on the machine that runs Claude or Codex."
