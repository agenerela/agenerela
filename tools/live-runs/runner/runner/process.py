"""Starting a block's process, watching it against its estimate, and killing it with its children.

Unity in batch mode starts helpers of its own (the licensing client, the shader compiler, the
build backend), so a kill takes the whole tree: taskkill /T on Windows, the process group elsewhere.
"""
import os
import re
import signal
import subprocess
import sys
import time
from pathlib import Path

from .common import RunnerError

WINDOWS = sys.platform == "win32"


def unity_exe(project_dir):
    """The editor ProjectVersion.txt pins: AGENERELA_UNITY if set, else the Hub's default folder."""
    if os.environ.get("AGENERELA_UNITY"):
        return os.environ["AGENERELA_UNITY"]
    version_file = Path(project_dir) / "ProjectSettings" / "ProjectVersion.txt"
    try:
        match = re.search(r"m_EditorVersion:\s*(\S+)", version_file.read_text(encoding="utf-8"))
    except OSError:
        raise RunnerError(f"no {version_file}: is project right in the request?")
    version = match.group(1) if match else "?"
    if WINDOWS:
        exe = Path(os.environ.get("ProgramFiles", r"C:\Program Files")) / "Unity" / "Hub" / "Editor" / version / "Editor" / "Unity.exe"
    elif sys.platform == "darwin":
        exe = Path("/Applications/Unity/Hub/Editor") / version / "Unity.app/Contents/MacOS/Unity"
    else:
        exe = Path.home() / "Unity" / "Hub" / "Editor" / version / "Editor" / "Unity"
    if not exe.is_file():
        raise RunnerError(f"Unity {version} is not at {exe}: install it through the Hub, or set AGENERELA_UNITY")
    return str(exe)


def start(args, env, cwd, stdout_path=None):
    """Start a block in its own process group, its output to stdout_path (or nowhere)."""
    out = open(str(stdout_path), "ab") if stdout_path else subprocess.DEVNULL
    try:
        kwargs = {"creationflags": subprocess.CREATE_NEW_PROCESS_GROUP} if WINDOWS else {"start_new_session": True}
        return subprocess.Popen([str(a) for a in args], cwd=str(cwd), env=env, stdin=subprocess.DEVNULL,
                                stdout=out, stderr=subprocess.STDOUT, **kwargs)
    except OSError as e:
        raise RunnerError(f"cannot start {args[0]}: {e}")
    finally:
        if stdout_path:
            out.close()


def watch(proc, limit_seconds, poll=1.0, tick=None, interrupt=None):
    """Wait for the process; ("exit", code), ("timeout", None) after killing it at the limit, or
    ("stopped", why) after killing it because interrupt() gave a reason (Stop now, the tray closing)."""
    started = time.monotonic()
    while True:
        code = proc.poll()
        if code is not None:
            return "exit", code
        why = interrupt() if interrupt else None
        if why:
            kill(proc.pid)
            try:
                proc.wait(timeout=30)
            except subprocess.TimeoutExpired:
                pass
            return "stopped", why
        if time.monotonic() - started > limit_seconds:
            kill(proc.pid)
            try:
                proc.wait(timeout=30)
            except subprocess.TimeoutExpired:
                pass
            return "timeout", None
        if tick:
            tick()
        time.sleep(poll)


def kill(pid):
    if WINDOWS:
        subprocess.run(["taskkill", "/PID", str(pid), "/T", "/F"], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    else:
        try:
            os.killpg(os.getpgid(pid), signal.SIGKILL)
        except (ProcessLookupError, PermissionError):
            pass


def image(pid):
    """The running program's file name as the system sees it (python.exe can start python3.13.exe), or None."""
    if WINDOWS:
        done = subprocess.run(["tasklist", "/FI", f"PID eq {pid}", "/FO", "CSV", "/NH"],
                              stdout=subprocess.PIPE, stderr=subprocess.DEVNULL, universal_newlines=True)
        line = done.stdout.strip()
        return line.split('","')[0].strip('"') if line.startswith('"') else None
    try:
        return Path(os.readlink(f"/proc/{pid}/exe")).name
    except OSError:
        return None


def running(pid):
    """Whether a process id is still running; cheap enough to ask every second (no tasklist)."""
    if not pid:
        return False
    if WINDOWS:
        import ctypes
        kernel32 = ctypes.windll.kernel32
        handle = kernel32.OpenProcess(0x1000, False, int(pid))  # PROCESS_QUERY_LIMITED_INFORMATION
        if not handle:
            return False
        try:
            code = ctypes.c_ulong()
            return bool(kernel32.GetExitCodeProcess(handle, ctypes.byref(code))) and code.value == 259  # STILL_ACTIVE
        finally:
            kernel32.CloseHandle(handle)
    try:
        os.kill(int(pid), 0)
        return True
    except ProcessLookupError:
        return False
    except PermissionError:
        return True


def alive(pid, exe=None):
    """Whether pid still runs, and is the same program (exe, a file name), so a reused pid is left alone."""
    if WINDOWS:
        done = subprocess.run(["tasklist", "/FI", f"PID eq {pid}", "/FO", "CSV", "/NH"],
                              stdout=subprocess.PIPE, stderr=subprocess.DEVNULL, universal_newlines=True)
        line = done.stdout.strip()
        if not line.startswith('"'):
            return False
        name = line.split('","')[0].strip('"')
        return exe is None or name.lower() == Path(exe).name.lower()
    try:
        os.kill(pid, 0)
    except ProcessLookupError:
        return False
    except PermissionError:
        return True
    if exe is None:
        return True
    try:
        return Path(os.readlink(f"/proc/{pid}/exe")).name == Path(exe).name
    except OSError:
        return True


def log_mentions(path, patterns):
    """The first pattern the log contains, or None."""
    try:
        text = Path(path).read_text(encoding="utf-8", errors="replace")
    except OSError:
        return None
    for p in patterns:
        if p and p in text:
            return p
    return None
