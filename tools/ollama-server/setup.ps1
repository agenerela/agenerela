# Prepares this folder on the RTX 5080 machine for the Agenerela Ollama stack.
# Run it from PowerShell in this folder:
#
#   powershell -ExecutionPolicy Bypass -File .\setup.ps1
#
# It checks Docker and the NVIDIA driver, makes sure this stack's container names and
# port do not clash with anything already on the machine, and creates .env with a
# fresh random key, pointing AGENERELA_OLLAMA_URL at this machine. It only reads Docker's
# state: it starts, stops and changes no container, and downloads nothing. In an existing
# .env it only fills an empty AGENERELA_OLLAMA_URL. Safe to re-run.
#
# Keep this file plain ASCII: Windows PowerShell 5.1 reads it in the ANSI code page.

Set-Location -LiteralPath $PSScriptRoot
$script:problems = 0
$project = 'agenerela-ollama'
$ourContainers = 'agenerela-ollama', 'agenerela-ollama-gateway'

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

Write-Host ""
Write-Host "Docker and GPU"

$dockerUp = $false
if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    Fail "docker was not found. Install Docker Desktop first."
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
    $gpu = (nvidia-smi --query-gpu=name,memory.total,driver_version --format=csv,noheader 2>&1) -join '; '
    if ($LASTEXITCODE -ne 0) { Fail "nvidia-smi failed: $gpu" } else { Ok "GPU: $gpu" }
}

$containers = @()
if ($dockerUp) {
    foreach ($line in (docker ps -a --format '{{.Names}}|{{.State}}|{{.Ports}}|{{.Labels}}')) {
        $f = $line -split '\|', 4
        if ($f.Count -lt 4) { continue }
        $owner = if ($f[3] -match 'com\.docker\.compose\.project=([^,]*)') { $Matches[1] } else { '' }
        $containers += [pscustomobject]@{ Name = $f[0]; State = $f[1]; Ports = $f[2]; Project = $owner }
    }
    foreach ($c in @($containers | Where-Object { $ourContainers -contains $_.Name -and $_.Project -ne $project })) {
        Fail "A container named '$($c.Name)' already exists and does not belong to this stack. Rename or remove it yourself if it is yours; this stack will not."
    }
    $ours = @($containers | Where-Object { $_.Project -eq $project })
    if ($ours.Count -gt 0) { Ok "This stack already has: $(($ours | ForEach-Object { "$($_.Name) ($($_.State))" }) -join ', ')" }
}

Write-Host ""
Write-Host "Settings"

$envFile = Join-Path $PSScriptRoot '.env'
if (Test-Path -LiteralPath $envFile) {
    Ok ".env already exists; its key is left unchanged"
} else {
    $bytes = New-Object byte[] 32
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    $rng.GetBytes($bytes)
    $rng.Dispose()
    $key = -join ($bytes | ForEach-Object { $_.ToString('x2') })
    $template = Get-Content -LiteralPath (Join-Path $PSScriptRoot '.env.example') -Raw -Encoding UTF8
    $content = $template -replace '(?m)^AGENERELA_OLLAMA_API_KEY=[^\r\n]*', "AGENERELA_OLLAMA_API_KEY=$key"
    [System.IO.File]::WriteAllText($envFile, $content, (New-Object System.Text.UTF8Encoding $false))
    Ok "Created .env with a new random AGENERELA_OLLAMA_API_KEY"
}

$settings = Read-DotEnv $envFile
if ($settings['AGENERELA_OLLAMA_API_KEY'].Length -lt 32) { Fail "AGENERELA_OLLAMA_API_KEY in .env is shorter than 32 characters. Delete .env and run this again." }
$apiPort = if ($settings['OLLAMA_API_PORT']) { [int]$settings['OLLAMA_API_PORT'] } else { 11436 }

# On the server, the client lines point at this machine, so ollama_remote.py works here too.
# Only an empty AGENERELA_OLLAMA_URL is filled in; nothing else in .env is touched.
if (-not $settings['AGENERELA_OLLAMA_URL']) {
    $text = [System.IO.File]::ReadAllText($envFile)
    $text = $text -replace '(?m)^AGENERELA_OLLAMA_URL=[ \t]*(?=\r?$)', "AGENERELA_OLLAMA_URL=http://127.0.0.1:$apiPort"
    [System.IO.File]::WriteAllText($envFile, $text, (New-Object System.Text.UTF8Encoding $false))
    Ok "Set AGENERELA_OLLAMA_URL in .env to this machine, http://127.0.0.1:$apiPort"
}

Write-Host ""
Write-Host "Port"

$publisher = $containers | Where-Object { $_.Ports -match ":$apiPort->" } | Select-Object -First 1
$listener = Get-NetTCPConnection -State Listen -LocalPort $apiPort -ErrorAction SilentlyContinue | Select-Object -First 1
if ($publisher -and $publisher.Project -eq $project) {
    Ok "Port $apiPort is this stack's own ('$($publisher.Name)' is running)"
} elseif ($publisher) {
    Fail "Port $apiPort is taken by container '$($publisher.Name)', which is not part of this stack. Pick a free OLLAMA_API_PORT in .env; do not change that container."
} elseif ($listener) {
    $process = Get-Process -Id $listener.OwningProcess -ErrorAction SilentlyContinue
    Fail "Port $apiPort is already used by $($process.ProcessName). Pick a free OLLAMA_API_PORT in .env."
} else {
    Ok "Port $apiPort is free"
}
if (Get-NetTCPConnection -State Listen -LocalPort 11434 -ErrorAction SilentlyContinue) {
    Ok "Port 11434 is in use, likely by the Ollama app. This stack does not use it."
}

Write-Host ""
Write-Host "Addresses other machines can use for AGENERELA_OLLAMA_URL"
$addresses = Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue |
    Where-Object { $_.IPAddress -notlike '127.*' -and $_.IPAddress -notlike '169.254.*' -and $_.InterfaceAlias -notmatch 'vEthernet|WSL|Loopback' }
foreach ($address in $addresses) {
    Write-Host ("  http://{0}:{1}   ({2})" -f $address.IPAddress, $apiPort, $address.InterfaceAlias)
}
Write-Host ("  http://127.0.0.1:{0}   (this machine)" -f $apiPort)

Write-Host ""
if ($script:problems -gt 0) {
    Write-Host "$($script:problems) problem(s) above. Fix them, then run this again." -ForegroundColor Red
    exit 1
}
Write-Host "Ready. Next, from this folder (README, steps 3 to 5):" -ForegroundColor Green
Write-Host "  docker compose up -d"
Write-Host "  docker exec agenerela-ollama ollama pull qwen3.5:2b"
Write-Host "  python ollama_remote.py check"
Write-Host "Then copy the AGENERELA_OLLAMA_API_KEY line from .env into tools\ollama-server\.env on the client machine."
