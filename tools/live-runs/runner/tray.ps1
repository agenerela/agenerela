<#
.SYNOPSIS
    The run queue's tray app: runs `runq.py serve` while it is open, and puts it under your control.

.DESCRIPTION
    A coloured dot in the notification area shows what the runner on this machine is doing:
    green playing a block, amber paused or waiting for the card, grey idle, red stopped or failing.
    Right-click it to pause after the block in hand, stop the block now (the GPU is free within
    seconds; the block plays again later from its start), resume, or quit.

    Game mode (on by default) stops the block in hand and pauses the runner as soon as a game runs:
    a program started from a game library (Steam, Epic, Xbox, GOG, Riot, EA, Ubisoft), a program on
    your own list, or any full-screen window. It resumes a few minutes after the game closes.
    Settings are in <work>\tray.json; the menu opens it.

    Closing the tray stops the runner: nothing plays while the tray is not running.
    Start it with tray.cmd beside this file, or let the menu's "Start with Windows" do it at logon.

    No install: Windows PowerShell 5.1 and the Python that runs runq.py. ASCII only, because
    PowerShell 5.1 reads a script without a byte-order mark in the ANSI code page.

.PARAMETER Work
    The runner's work folder (default: what `runq.py where` says).
.PARAMETER Local
    Serve QUEUE.json and the requests from this folder instead of origin/test, and push nothing.
    For trying the tray out.
.PARAMETER SelfTest
    Check the parts that need no screen (settings, the control files, game detection with a fake
    game) and exit 0 when they pass.
#>
param(
    [string]$Work,
    [string]$Local,
    [switch]$NoPush,
    [switch]$SelfTest
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2

$Kit = Split-Path -Parent $MyInvocation.MyCommand.Path
$Runq = Join-Path $Kit 'runq.py'
$TrayScript = $MyInvocation.MyCommand.Path

# ---------------------------------------------------------------------------------------------
# Finding Python and the work folder
# ---------------------------------------------------------------------------------------------

function Find-Python {
    foreach ($name in @('python', 'py')) {
        $c = Get-Command $name -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($c) { return $c.Source }
    }
    throw 'Python 3.8 or later is needed and was not found on PATH.'
}

$Python = Find-Python

function Get-RunnerPaths {
    $a = @($Runq, 'where')
    if ($Work) { $a += @('--work', $Work) }
    $json = & $Python @a
    if ($LASTEXITCODE -ne 0) { throw "runq.py where failed: $json" }
    return ($json | ConvertFrom-Json)
}

$Paths = Get-RunnerPaths
$WorkDir = $Paths.work
New-Item -ItemType Directory -Force -Path $WorkDir | Out-Null
$PauseFile = Join-Path $WorkDir 'pause'
$StopFile = Join-Path $WorkDir 'stop'
$StatusFile = Join-Path $WorkDir 'status.json'
$SettingsFile = Join-Path $WorkDir 'tray.json'
$CommandFile = Join-Path $WorkDir 'tray-command'
$ServeLog = Join-Path $WorkDir 'serve.log'
$TrayLog = Join-Path $WorkDir 'tray.log'
$ResultsDir = $Paths.results

function Write-TrayLog([string]$text) {
    try { Add-Content -LiteralPath $TrayLog -Value ("{0}  {1}" -f (Get-Date -Format s), $text) -Encoding UTF8 } catch { }
}

# ---------------------------------------------------------------------------------------------
# Settings
# ---------------------------------------------------------------------------------------------

function Get-DefaultSettings {
    [ordered]@{
        game_mode            = $true
        fullscreen_is_a_game = $true
        resume_after_minutes = 3
        start_runner         = $true
        game_folders         = @('\steamapps\common\', '\Epic Games\', '\XboxGames\', '\GOG Games\',
                                 '\Riot Games\', '\EA Games\', '\Ubisoft Game Launcher\games\')
        games                = @()
        # programs that run from a game library or full screen but are not games
        never_a_game         = @('wallpaper32.exe', 'wallpaper64.exe', 'webwallpaper32.exe', 'ui32.exe',
                                 'steamwebhelper.exe', 'crashpad_handler.exe', 'UnityCrashHandler64.exe',
                                 'claude.exe', 'chrome.exe', 'msedge.exe', 'firefox.exe', 'opera.exe',
                                 'Code.exe', 'Cursor.exe', 'WindowsTerminal.exe', 'mstsc.exe',
                                 'ApplicationFrameHost.exe', 'TextInputHost.exe', 'SearchHost.exe',
                                 'StartMenuExperienceHost.exe', 'ShellExperienceHost.exe', 'LockApp.exe')
    }
}

function Read-Settings {
    $s = Get-DefaultSettings
    if (Test-Path -LiteralPath $SettingsFile) {
        try {
            $saved = Get-Content -LiteralPath $SettingsFile -Raw -Encoding UTF8 | ConvertFrom-Json
            foreach ($p in $saved.PSObject.Properties) {
                if ($s.Contains($p.Name)) {
                    if ($s[$p.Name] -is [array]) { $s[$p.Name] = @($p.Value) } else { $s[$p.Name] = $p.Value }
                }
            }
        } catch {
            Write-TrayLog "tray.json could not be read, using the defaults: $_"
        }
    }
    return $s
}

function Save-Settings($s) {
    ($s | ConvertTo-Json -Depth 4) | Set-Content -LiteralPath $SettingsFile -Encoding UTF8
}

# ---------------------------------------------------------------------------------------------
# The control files the runner reads: pause (after the block in hand) and stop (now)
# ---------------------------------------------------------------------------------------------

function Write-Note([string]$path, [string]$by, [string]$why) {
    $text = "by: $by`nwhy: $why`nsince: $(Get-Date -Format s)`n"
    [System.IO.File]::WriteAllText($path, $text)
}

function Read-NoteBy([string]$path) {
    if (-not (Test-Path -LiteralPath $path)) { return $null }
    try {
        foreach ($line in Get-Content -LiteralPath $path) {
            if ($line -like 'by:*') { return $line.Substring(3).Trim() }
        }
    } catch { }
    return 'someone'
}

function Remove-Note([string]$path) {
    if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Force -ErrorAction SilentlyContinue }
}

function Invoke-Pause([string]$by, [string]$why) {
    Write-Note $PauseFile $by $why
}

function Invoke-StopNow([string]$by, [string]$why) {
    # pause first, so the runner never starts the next block in between
    Write-Note $PauseFile $by $why
    Write-Note $StopFile $by $why
}

function Invoke-Resume {
    Remove-Note $StopFile
    Remove-Note $PauseFile
}

# ---------------------------------------------------------------------------------------------
# Game detection
# ---------------------------------------------------------------------------------------------

if (-not ('AgenerelaTray.Native' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace AgenerelaTray {
    public static class Native {
        [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public class MONITORINFO { public int cbSize = Marshal.SizeOf(typeof(MONITORINFO)); public RECT rcMonitor; public RECT rcWork; public int dwFlags; }

        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
        [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr h, uint flags);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool GetMonitorInfo(IntPtr m, [In, Out] MONITORINFO mi);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
        [DllImport("shell32.dll")] static extern int SHQueryUserNotificationState(out int state);
        [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
        [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern bool QueryFullProcessImageName(IntPtr h, uint flags, StringBuilder name, ref uint size);

        // The process id of a foreground window that covers its whole monitor, or 0.
        public static uint FullscreenPid() {
            IntPtr h = GetForegroundWindow();
            if (h == IntPtr.Zero) return 0;
            var cls = new StringBuilder(256);
            GetClassName(h, cls, 256);
            string c = cls.ToString();
            if (c == "Progman" || c == "WorkerW" || c == "Shell_TrayWnd" || c == "Shell_SecondaryTrayWnd") return 0;
            uint pid;
            GetWindowThreadProcessId(h, out pid);
            int state;
            if (SHQueryUserNotificationState(out state) == 0 && state == 3) return pid;  // a Direct3D full-screen app
            RECT r;
            if (!GetWindowRect(h, out r)) return 0;
            var mi = new MONITORINFO();
            if (!GetMonitorInfo(MonitorFromWindow(h, 2), mi)) return 0;
            bool covers = r.Left <= mi.rcMonitor.Left && r.Top <= mi.rcMonitor.Top
                       && r.Right >= mi.rcMonitor.Right && r.Bottom >= mi.rcMonitor.Bottom;
            return covers ? pid : 0;
        }

        public static string PathOf(uint pid) {
            IntPtr h = OpenProcess(0x1000, false, pid);  // PROCESS_QUERY_LIMITED_INFORMATION
            if (h == IntPtr.Zero) return null;
            try {
                var sb = new StringBuilder(1024);
                uint size = (uint)sb.Capacity;
                return QueryFullProcessImageName(h, 0, sb, ref size) ? sb.ToString() : null;
            } finally { CloseHandle(h); }
        }

        // Every running program's full path, quickly (no WMI).
        public static string[] ProcessPaths() {
            var list = new List<string>();
            foreach (var p in Process.GetProcesses()) {
                try { string path = PathOf((uint)p.Id); if (path != null) list.Add(path); }
                catch { }
                finally { p.Dispose(); }
            }
            return list.ToArray();
        }
    }
}
'@
}

function Test-Ignored($settings, [string]$name) {
    foreach ($n in @($settings.never_a_game)) { if ($n -and $name -ieq $n) { return $true } }
    return $false
}

# The reason a game is running ("fakegame.exe from a game library"), or $null.
function Find-Game($settings) {
    $self = [System.Diagnostics.Process]::GetCurrentProcess().Id
    $paths = [AgenerelaTray.Native]::ProcessPaths()
    foreach ($path in $paths) {
        $name = [System.IO.Path]::GetFileName($path)
        if (Test-Ignored $settings $name) { continue }
        foreach ($g in @($settings.games)) {
            if ($g -and $name -ieq $g) { return "$name (on your game list)" }
        }
        foreach ($folder in @($settings.game_folders)) {
            if ($folder -and $path.IndexOf($folder, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
                return "$name (from a game library)"
            }
        }
    }
    if ($settings.fullscreen_is_a_game) {
        $fpid = [AgenerelaTray.Native]::FullscreenPid()
        if ($fpid -ne 0 -and $fpid -ne $self) {
            $path = [AgenerelaTray.Native]::PathOf($fpid)
            if ($path) {
                $name = [System.IO.Path]::GetFileName($path)
                if (-not (Test-Ignored $settings $name) -and $name -ine 'explorer.exe') {
                    return "$name (full screen)"
                }
            }
        }
    }
    return $null
}

function Get-GameName([string]$reason) {
    if (-not $reason) { return $null }
    return ($reason -split ' ')[0]
}

# ---------------------------------------------------------------------------------------------
# Self-test: everything but the screen
# ---------------------------------------------------------------------------------------------

if ($SelfTest) {
    $failed = 0
    function Check([string]$what, [bool]$ok) {
        if ($ok) { Write-Output "PASS  $what" } else { Write-Output "FAIL  $what"; $script:failed++ }
    }
    $s = Read-Settings
    Check 'settings load, game mode on by default' ($s.game_mode -eq $true)
    Invoke-Pause 'user' 'self-test'
    Check 'pause writes the file the runner reads' ((Read-NoteBy $PauseFile) -eq 'user')
    Invoke-StopNow 'game' 'self-test'
    Check 'stop now writes pause and stop' ((Test-Path $StopFile) -and (Read-NoteBy $PauseFile) -eq 'game')
    Invoke-Resume
    Check 'resume removes both' (-not (Test-Path $StopFile) -and -not (Test-Path $PauseFile))

    $root = Join-Path ([System.IO.Path]::GetTempPath()) ('agenerela-tray-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
    $gameDir = Join-Path $root 'steamapps\common\FakeGame'
    New-Item -ItemType Directory -Force -Path $gameDir | Out-Null
    $fake = Join-Path $gameDir 'fakegame.exe'
    Copy-Item -LiteralPath (Join-Path $env:SystemRoot 'System32\PING.EXE') -Destination $fake
    $before = Find-Game $s
    Check "no game before the fake one starts (found: $before)" ($null -eq $before -or $before -notlike 'fakegame.exe*')
    $p = Start-Process -FilePath $fake -ArgumentList '-n', '30', '127.0.0.1' -WindowStyle Hidden -PassThru
    Start-Sleep -Milliseconds 800
    $found = Find-Game $s
    Check "a program in a steamapps\common folder is a game (found: $found)" ($found -like 'fakegame.exe*')
    $s.never_a_game = @($s.never_a_game) + 'fakegame.exe'
    $ignored = Find-Game $s
    Check 'never_a_game skips it' ($null -eq $ignored -or $ignored -notlike 'fakegame.exe*')
    Stop-Process -Id $p.Id -Force
    Start-Sleep -Milliseconds 500
    $s = Read-Settings
    $after = Find-Game $s
    Check 'no game once it closes' ($null -eq $after -or $after -notlike 'fakegame.exe*')
    $s2 = Read-Settings; $s2.games = @('ping.exe')
    $p = Start-Process -FilePath (Join-Path $env:SystemRoot 'System32\PING.EXE') -ArgumentList '-n', '30', '127.0.0.1' -WindowStyle Hidden -PassThru
    Start-Sleep -Milliseconds 800
    Check 'a program on the game list is a game' ((Find-Game $s2) -like 'PING.EXE*')
    Stop-Process -Id $p.Id -Force
    Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
    Check "full-screen check runs (foreground full-screen pid: $([AgenerelaTray.Native]::FullscreenPid()))" $true
    if ($failed) { Write-Output "$failed failed"; exit 1 }
    Write-Output 'all passed'
    exit 0
}

# ---------------------------------------------------------------------------------------------
# One tray at a time
# ---------------------------------------------------------------------------------------------

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[System.Windows.Forms.Application]::EnableVisualStyles()

$created = $false
$mutex = New-Object System.Threading.Mutex($true, 'Local\AgenerelaRunnerTray', [ref]$created)
if (-not $created) {
    [System.Windows.Forms.MessageBox]::Show('The Agenerela runner tray is already running: look for its dot in the notification area.',
        'Agenerela runner') | Out-Null
    exit 0
}

# ---------------------------------------------------------------------------------------------
# The runner process
# ---------------------------------------------------------------------------------------------

$script:Serve = $null
$script:WeStopped = $false
$script:Restarts = New-Object System.Collections.Generic.List[datetime]

function Test-ServeRunning {
    return ($null -ne $script:Serve -and -not $script:Serve.HasExited)
}

function Start-Serve {
    if (Test-ServeRunning) { return }
    Remove-Note $StopFile
    $q = { param($x) '"' + $x + '"' }
    $argList = @((& $q $Runq), 'serve', '--parent-pid', $PID, '--log', (& $q $ServeLog))
    if ($Work) { $argList += @('--work', (& $q $Work)) }
    if ($Local) { $argList += @('--local', (& $q $Local)) }
    if ($NoPush) { $argList += '--no-push' }
    $script:Serve = Start-Process -FilePath $Python -ArgumentList $argList -WindowStyle Hidden -PassThru
    $null = $script:Serve.Handle  # keep the handle, so the exit code can be read later
    $script:WeStopped = $false
    Write-TrayLog "runner started, pid $($script:Serve.Id)"
}

function Wait-CardFree([int]$seconds) {
    $deadline = (Get-Date).AddSeconds($seconds)
    while ((Get-Date) -lt $deadline) {
        $st = Read-Status
        if (-not (Test-ServeRunning) -or ($st -and -not $st.card_in_use -and $st.state -ne 'running')) { return $true }
        Start-Sleep -Milliseconds 500
        [System.Windows.Forms.Application]::DoEvents()
    }
    return $false
}

function Stop-Serve([string]$why) {
    if (-not (Test-ServeRunning)) { return }
    $script:WeStopped = $true
    Invoke-StopNow 'quit' $why
    $null = Wait-CardFree 60
    if (Test-ServeRunning) {
        # taskkill writes to stderr when the process is already gone; never let that stop a quit
        $old = $ErrorActionPreference
        $ErrorActionPreference = 'SilentlyContinue'
        try { & taskkill.exe /PID $script:Serve.Id /T /F 2>&1 | Out-Null } catch { }
        $ErrorActionPreference = $old
    }
    Write-TrayLog "runner stopped: $why"
}

# ---------------------------------------------------------------------------------------------
# What the runner is doing
# ---------------------------------------------------------------------------------------------

function Read-Status {
    if (-not (Test-Path -LiteralPath $StatusFile)) { return $null }
    for ($i = 0; $i -lt 3; $i++) {
        try { return (Get-Content -LiteralPath $StatusFile -Raw -Encoding UTF8 | ConvertFrom-Json) }
        catch { Start-Sleep -Milliseconds 100 }
    }
    return $null
}

function Get-View {
    # (colour, one-line state, longer text)
    $st = Read-Status
    $pauseBy = Read-NoteBy $PauseFile
    if (-not (Test-ServeRunning)) {
        return @('red', 'Runner stopped', 'The runner is not running. Right-click and choose Start the runner, or see the log.')
    }
    if ($null -eq $st) { return @('grey', 'Starting', 'The runner is starting.') }
    $hand = ''
    if ($st.request) { $hand = $st.request; if ($st.block) { $hand += ' / ' + $st.block } }
    $progress = ''
    if ($st.blocks_total) { $progress = " ($($st.blocks_done) of $($st.blocks_total) blocks done, about $($st.minutes_left) min left)" }
    switch ($st.state) {
        'running' {
            if ($pauseBy) { return @('amber', "Pausing after $($st.block)", "Pausing after the block in hand: $hand$progress") }
            return @('green', "Playing $($st.block)", "Playing $hand$progress")
        }
        'waiting' { return @('amber', 'Waiting for the GPU', "Waiting for the card: $($st.waiting). Next: $hand") }
        'paused' { return @('amber', 'Paused', "Paused: $($st.waiting)") }
        'error' { return @('red', 'Error', "Error: $($st.last_error). It tries again in a minute.") }
        'stopped' { return @('red', 'Stopped', 'Stopped.') }
        default {
            if ($pauseBy) { return @('amber', 'Paused', "Paused ($pauseBy).") }
            $text = 'Idle: nothing owed in the queue.'
            if ($st.waiting -and $st.waiting -ne 'nothing owed') { $text = "Idle: $($st.waiting)" }
            if ($st.last_error) { $text += " Last problem: $($st.last_error)" }
            return @('grey', 'Idle', $text)
        }
    }
}

# ---------------------------------------------------------------------------------------------
# The icon
# ---------------------------------------------------------------------------------------------

$Colours = @{
    green = [System.Drawing.Color]::FromArgb(46, 158, 68)
    amber = [System.Drawing.Color]::FromArgb(224, 161, 0)
    grey  = [System.Drawing.Color]::FromArgb(138, 143, 152)
    red   = [System.Drawing.Color]::FromArgb(209, 52, 56)
}
$Icons = @{}
foreach ($k in $Colours.Keys) {
    $bmp = New-Object System.Drawing.Bitmap 32, 32
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)
    $brush = New-Object System.Drawing.SolidBrush $Colours[$k]
    $g.FillEllipse($brush, 3, 3, 26, 26)
    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::White), 2
    $g.DrawEllipse($pen, 3, 3, 26, 26)
    $font = New-Object System.Drawing.Font 'Segoe UI', 13, ([System.Drawing.FontStyle]::Bold), ([System.Drawing.GraphicsUnit]::Pixel)
    $fmt = New-Object System.Drawing.StringFormat
    $fmt.Alignment = 'Center'; $fmt.LineAlignment = 'Center'
    $g.DrawString('A', $font, [System.Drawing.Brushes]::White, (New-Object System.Drawing.RectangleF 3, 4, 26, 26), $fmt)
    $g.Dispose()
    $Icons[$k] = [System.Drawing.Icon]::FromHandle($bmp.GetHicon())
}

$tray = New-Object System.Windows.Forms.NotifyIcon
$tray.Icon = $Icons['grey']
$tray.Text = 'Agenerela runner: starting'
$tray.Visible = $true

function Show-Note([string]$title, [string]$text) {
    $tray.ShowBalloonTip(6000, $title, $text, [System.Windows.Forms.ToolTipIcon]::Info)
}

# ---------------------------------------------------------------------------------------------
# Game mode
# ---------------------------------------------------------------------------------------------

$script:Settings = Read-Settings
$script:Game = $null            # the game seen last tick
$script:GameGoneAt = $null      # when it closed
$script:Override = $null        # a game you resumed over: game mode leaves it alone until it closes
$script:NextScan = Get-Date

function Update-GameMode {
    if ((Get-Date) -lt $script:NextScan) { return }
    $script:NextScan = (Get-Date).AddSeconds(4)
    $s = $script:Settings
    $pauseBy = Read-NoteBy $PauseFile
    if (-not $s.game_mode) {
        $script:Game = $null
        if ($pauseBy -eq 'game') { Invoke-Resume }
        return
    }
    $found = Find-Game $s
    $name = Get-GameName $found
    if ($script:Override -and $name -ne $script:Override) { $script:Override = $null }
    if ($found -and $name -ne $script:Override) {
        $script:GameGoneAt = $null
        if ($pauseBy -ne 'game' -and $pauseBy -ne 'user' -and $pauseBy -ne 'stop') {
            Invoke-StopNow 'game' "game mode: $found"
            Write-TrayLog "game mode: paused for $found"
            Show-Note 'Runner paused for your game' "$found is running, so the block in hand was stopped and the GPU is free. It resumes $($s.resume_after_minutes) min after the game closes."
        }
        $script:Game = $found
        return
    }
    if ($script:Game -and -not $found) {
        $script:Game = $null
        $script:GameGoneAt = Get-Date
    }
    if ($pauseBy -eq 'game' -and -not $found) {
        if (-not $script:GameGoneAt) { $script:GameGoneAt = Get-Date }
        if (((Get-Date) - $script:GameGoneAt).TotalMinutes -ge [double]$s.resume_after_minutes) {
            Invoke-Resume
            $script:GameGoneAt = $null
            Write-TrayLog 'game mode: resumed'
            Show-Note 'Runner resumed' 'The game closed, so the runner continues with the next block.'
        }
    }
}

# ---------------------------------------------------------------------------------------------
# The menu
# ---------------------------------------------------------------------------------------------

function Set-StartWithWindows([bool]$on) {
    $lnk = Join-Path ([Environment]::GetFolderPath('Startup')) 'Agenerela runner.lnk'
    if (-not $on) { Remove-Item -LiteralPath $lnk -Force -ErrorAction SilentlyContinue; return }
    $shell = New-Object -ComObject WScript.Shell
    $sc = $shell.CreateShortcut($lnk)
    $sc.TargetPath = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
    $a = "-NoProfile -STA -WindowStyle Hidden -ExecutionPolicy Bypass -File `"$TrayScript`""
    if ($Work) { $a += " -Work `"$Work`"" }
    $sc.Arguments = $a
    $sc.WorkingDirectory = $Kit
    $sc.Description = 'Agenerela run queue tray'
    $sc.Save()
}

function Test-StartWithWindows {
    return (Test-Path -LiteralPath (Join-Path ([Environment]::GetFolderPath('Startup')) 'Agenerela runner.lnk'))
}

# ---------------------------------------------------------------------------------------------
# The queue window: runq.py status, refreshed, with the queue's own buttons
# ---------------------------------------------------------------------------------------------

$script:QueueForm = $null

function Start-Runq([string[]]$runqArgs) {
    # run runq.py without blocking the tray; the caller polls .HasExited
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $Python
    $all = @('"' + $Runq + '"') + @($runqArgs | ForEach-Object { if ($_ -match '\s') { '"' + $_ + '"' } else { $_ } })
    $psi.Arguments = ($all -join ' ')
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.RedirectStandardInput = $true
    $psi.StandardOutputEncoding = [System.Text.Encoding]::UTF8
    $p = [System.Diagnostics.Process]::Start($psi)
    $p.StandardInput.Close()
    $out = $p.StandardOutput.ReadToEndAsync()
    $err = $p.StandardError.ReadToEndAsync()
    return @{ Process = $p; Out = $out; Err = $err }
}

function Show-Queue {
    if ($script:QueueForm -and -not $script:QueueForm.IsDisposed) { $script:QueueForm.Activate(); return }
    $f = New-Object System.Windows.Forms.Form
    $f.Text = 'Agenerela run queue'
    $f.Size = New-Object System.Drawing.Size 860, 480
    $f.StartPosition = 'CenterScreen'
    $f.Icon = $Icons['grey']
    $script:Qbox = New-Object System.Windows.Forms.TextBox
    $script:Qbox.Multiline = $true; $script:Qbox.ReadOnly = $true; $script:Qbox.ScrollBars = 'Both'; $script:Qbox.WordWrap = $false
    $script:Qbox.Font = New-Object System.Drawing.Font 'Consolas', 10
    $script:Qbox.Dock = 'Fill'
    $script:Qbox.BackColor = [System.Drawing.Color]::White
    $script:Qbar = New-Object System.Windows.Forms.FlowLayoutPanel
    $script:Qbar.Dock = 'Bottom'; $script:Qbar.Height = 40; $script:Qbar.Padding = New-Object System.Windows.Forms.Padding 6
    $script:Qlabel = New-Object System.Windows.Forms.Label
    $script:Qlabel.AutoSize = $true; $script:Qlabel.Padding = New-Object System.Windows.Forms.Padding 0, 7, 12, 0
    $f.Controls.Add($script:Qbox); $f.Controls.Add($script:Qbar)
    $script:QueueJob = $null
    $script:QueueAfter = $null

    $script:Qrefresh = {
        if ($script:QueueJob) { return }
        $script:Qlabel.Text = 'Reading the queue from test...'
        $statusArgs = @('status'); if ($Work) { $statusArgs += @('--work', $Work) }
        $script:QueueJob = Start-Runq $statusArgs
        $script:QueueAfter = $null
    }
    $script:Qchange = {
        param([string[]]$runqArgs, [string]$what)
        $ok = [System.Windows.Forms.MessageBox]::Show("$what`n`nThis is a commit on test that everyone sees. Push it?",
            'Agenerela run queue', 'YesNo', 'Question')
        if ($ok -ne 'Yes') { return }
        if ($script:QueueJob) { return }
        $script:Qlabel.Text = 'Pushing to test...'
        $script:QueueJob = Start-Runq ($runqArgs + '--yes')
        $script:QueueAfter = 'refresh'
    }
    $script:Qadd = {
        param([string]$text, [scriptblock]$click)
        $b = New-Object System.Windows.Forms.Button
        $b.Text = $text; $b.AutoSize = $true
        $b.Add_Click($click)
        [void]$script:Qbar.Controls.Add($b)
    }
    & $script:Qadd 'Refresh' $script:Qrefresh
    & $script:Qadd 'Pause the queue...' {
        Add-Type -AssemblyName Microsoft.VisualBasic
        $why = [Microsoft.VisualBasic.Interaction]::InputBox('Why pause the queue? (it goes in the commit message)', 'Pause the queue', 'playing a game')
        if ($why) { & $script:Qchange @('pause', $why) "Pause the queue for every machine: $why" }
    }
    & $script:Qadd 'Resume the queue' { & $script:Qchange @('resume') 'Resume the queue for every machine.' }
    & $script:Qadd 'Move a run to the front...' {
        Add-Type -AssemblyName Microsoft.VisualBasic
        $id = [Microsoft.VisualBasic.Interaction]::InputBox('The run request id, such as 007-free-orders-prompt-ab', 'Move to the front', '')
        if ($id) { & $script:Qchange @('front', $id.Trim()) "Move $($id.Trim()) to the front of the queue." }
    }
    [void]$script:Qbar.Controls.Add($script:Qlabel)

    $script:Qpoll = New-Object System.Windows.Forms.Timer
    $script:Qpoll.Interval = 500
    $script:QueueRefreshAt = (Get-Date).AddSeconds(60)
    $script:Qpoll.Add_Tick({
        try {
            if ($script:QueueJob -and $script:QueueJob.Process.HasExited) {
                $text = $script:QueueJob.Out.Result + $script:QueueJob.Err.Result
                $then = $script:QueueAfter
                $script:QueueJob = $null
                if ($then -eq 'refresh') {
                    $script:Qlabel.Text = ($text.Trim() -split "`n")[-1]
                    & $script:Qrefresh
                } else {
                    $script:Qbox.Text = $text.Replace("`r`n", "`n").Replace("`n", "`r`n")
                    $script:Qlabel.Text = 'Updated ' + (Get-Date -Format 'HH:mm:ss') + '; refreshes every minute'
                }
                $script:QueueRefreshAt = (Get-Date).AddSeconds(60)
            } elseif (-not $script:QueueJob -and (Get-Date) -gt $script:QueueRefreshAt) {
                & $script:Qrefresh
            }
        } catch { Write-TrayLog "queue window: $_" }
    })
    $f.Add_FormClosed({ $script:Qpoll.Stop(); $script:Qpoll.Dispose(); $script:QueueForm = $null })
    $script:QueueForm = $f
    $script:Qpoll.Start()
    & $script:Qrefresh
    $f.Show()
}

$menu = New-Object System.Windows.Forms.ContextMenuStrip
function Add-Item([string]$text, [scriptblock]$action) {
    $item = New-Object System.Windows.Forms.ToolStripMenuItem $text
    if ($action) { $item.Add_Click($action) }
    [void]$menu.Items.Add($item)
    return $item
}
function Add-Line { [void]$menu.Items.Add((New-Object System.Windows.Forms.ToolStripSeparator)) }

$miState = Add-Item 'Starting' $null
$miState.Enabled = $false
Add-Line
$miPause = Add-Item 'Pause after this block' {
    Invoke-Pause 'user' 'paused from the tray'
    Update-View
}
$miStop = Add-Item 'Stop now (free the GPU)' {
    Invoke-StopNow 'user' 'Stop now from the tray'
    Show-Note 'Stopping' 'The block in hand is stopped and set aside; it plays again from its start when you resume.'
    Update-View
}
$miResume = Add-Item 'Resume' {
    if ($script:Game) { $script:Override = Get-GameName $script:Game }
    Invoke-Resume
    if (-not (Test-ServeRunning)) { Start-Serve }
    Update-View
}
Add-Line
$miGameMode = Add-Item 'Game mode: pause while I play' {
    $script:Settings.game_mode = -not $script:Settings.game_mode
    Save-Settings $script:Settings
    $script:NextScan = Get-Date
    Update-GameMode
    Update-View
}
$miGame = Add-Item 'Game' $null
$miGame.Enabled = $false
$miNever = Add-Item 'Never count it as a game' {
    $name = Get-GameName $script:Game
    if ($name) {
        $script:Settings.never_a_game = @($script:Settings.never_a_game) + $name
        Save-Settings $script:Settings
        Show-Note 'Game mode' "$name no longer counts as a game. Settings are in tray.json."
        $script:Game = $null
        $script:NextScan = Get-Date
        Update-GameMode
        Update-View
    }
}
Add-Item 'Game mode settings...' {
    if (-not (Test-Path -LiteralPath $SettingsFile)) { Save-Settings $script:Settings }
    Start-Process notepad.exe -ArgumentList "`"$SettingsFile`""
} | Out-Null
Add-Line
Add-Item 'Show status' {
    $view = Get-View
    $st = Read-Status
    $more = ''
    if ($st) { $more = "`n`nRunner: $($st.runner)`nUpdated: $($st.time)" }
    [System.Windows.Forms.MessageBox]::Show($view[2] + $more, 'Agenerela runner') | Out-Null
} | Out-Null
Add-Item 'Show the queue...' { Show-Queue } | Out-Null
Add-Item 'Open results folder' {
    New-Item -ItemType Directory -Force -Path $ResultsDir | Out-Null
    Start-Process explorer.exe -ArgumentList "`"$ResultsDir`""
} | Out-Null
Add-Item 'Open the runner log' {
    if (Test-Path -LiteralPath $ServeLog) { Start-Process notepad.exe -ArgumentList "`"$ServeLog`"" }
} | Out-Null
Add-Line
$miStart = Add-Item 'Start the runner' { Start-Serve; Update-View }
$miStartup = Add-Item 'Start with Windows' {
    Set-StartWithWindows (-not (Test-StartWithWindows))
    Update-View
}
Add-Line
Add-Item 'Quit (stops the runner)' { Exit-Tray } | Out-Null

$menu.Add_Opening({ Update-View })
$tray.ContextMenuStrip = $menu
$tray.Add_MouseClick({
    param($sender, $e)
    if ($e.Button -eq [System.Windows.Forms.MouseButtons]::Left) {
        $view = Get-View
        $tray.ShowBalloonTip(5000, 'Agenerela runner', $view[2], [System.Windows.Forms.ToolTipIcon]::None)
    }
})

$script:LastColour = ''
$script:LastState = ''
function Update-View {
    $view = Get-View
    if ($view[0] -ne $script:LastColour) { $tray.Icon = $Icons[$view[0]]; $script:LastColour = $view[0] }
    $tip = 'Agenerela runner: ' + $view[1]
    if ($tip.Length -gt 63) { $tip = $tip.Substring(0, 60) + '...' }
    $tray.Text = $tip
    $miState.Text = $view[1]
    $pauseBy = Read-NoteBy $PauseFile
    $running = Test-ServeRunning
    $miPause.Enabled = $running -and -not $pauseBy
    $miStop.Enabled = $running -and -not (Test-Path -LiteralPath $StopFile)
    $miResume.Enabled = [bool]$pauseBy -or -not $running
    $miStart.Visible = -not $running
    $miGameMode.Checked = [bool]$script:Settings.game_mode
    $miStartup.Checked = Test-StartWithWindows
    if ($script:Game) {
        $miGame.Text = "Game: $($script:Game)"
        $miGame.Visible = $true; $miNever.Visible = $true
        $miNever.Text = "Never count $(Get-GameName $script:Game) as a game"
    } else {
        $miGame.Visible = $false; $miNever.Visible = $false
    }
    # say so when a block finishes or the runner stops by itself
    $st = Read-Status
    $key = if ($st) { "$($st.state)|$($st.request)|$($st.blocks_done)" } else { '' }
    if ($script:LastState -and $st -and $key -ne $script:LastState -and $st.state -eq 'finished') {
        Show-Note 'Run finished' "$($st.request): $($st.blocks_done) of $($st.blocks_total) blocks done."
    }
    $script:LastState = $key
}

# ---------------------------------------------------------------------------------------------
# The loop
# ---------------------------------------------------------------------------------------------

$script:Exiting = $false
function Exit-Tray {
    if ($script:Exiting) { return }
    $script:Exiting = $true
    $tray.Text = 'Agenerela runner: stopping'
    Stop-Serve 'the tray app closed'
    Remove-Note $StopFile
    if ((Read-NoteBy $PauseFile) -eq 'quit') { Remove-Note $PauseFile }
    $timer.Stop()
    $tray.Visible = $false
    $tray.Dispose()
    Write-TrayLog 'tray closed'
    [System.Windows.Forms.Application]::Exit()
}

function Invoke-TrayCommand {
    # tray-command: one word, for scripts and tests to do what the menu does
    if (-not (Test-Path -LiteralPath $CommandFile)) { return }
    $word = (Get-Content -LiteralPath $CommandFile -Raw).Trim().ToLowerInvariant()
    Remove-Note $CommandFile
    Write-TrayLog "command: $word"
    switch ($word) {
        'pause' { Invoke-Pause 'user' 'paused by tray-command' }
        'stop' { Invoke-StopNow 'user' 'Stop now by tray-command' }
        'resume' { if ($script:Game) { $script:Override = Get-GameName $script:Game }; Invoke-Resume }
        'quit' { Exit-Tray }
        'queue' { Show-Queue }
        'menu' { Update-View; $menu.Show([System.Windows.Forms.Cursor]::Position) }
    }
}

function On-Tick {
    try {
        Invoke-TrayCommand
        if ($script:Exiting) { return }
        Update-GameMode
        if (-not (Test-ServeRunning) -and $script:Serve -and -not $script:WeStopped) {
            $code = $script:Serve.ExitCode
            Write-TrayLog "runner exited by itself with code $code"
            $recent = @($script:Restarts | Where-Object { $_ -gt (Get-Date).AddMinutes(-10) })
            if ($recent.Count -lt 3) {
                $script:Restarts.Add((Get-Date))
                Start-Serve
            } else {
                $script:WeStopped = $true
                Show-Note 'Runner stopped' 'It exited three times in ten minutes. See the runner log from the menu.'
            }
        }
        Update-View
    } catch {
        Write-TrayLog "tick failed: $_"
    }
}

# A pause left by game mode or by quitting belongs to that moment, not to this start.
$by = Read-NoteBy $PauseFile
if ($by -eq 'game' -or $by -eq 'quit') { Remove-Note $PauseFile }
Remove-Note $StopFile
Remove-Note $CommandFile
Write-TrayLog "tray started (work $WorkDir$(if ($Local) { ", local queue $Local" }))"
if ($script:Settings.start_runner) { Start-Serve }

$timer = New-Object System.Windows.Forms.Timer
$timer.Interval = 1500
$timer.Add_Tick({ On-Tick })
$timer.Start()
On-Tick

$context = New-Object System.Windows.Forms.ApplicationContext
try {
    [System.Windows.Forms.Application]::Run($context)
} finally {
    if (Test-ServeRunning) { Stop-Serve 'the tray app ended' }
    $tray.Visible = $false
    $mutex.ReleaseMutex()
}
