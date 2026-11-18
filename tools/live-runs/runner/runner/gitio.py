"""Git: requests and the queue are read from origin/test; a request plays in a worktree at its commit;
results go back on a branch runs/<request id>, and only there.

The runner never pushes anything but refs/heads/runs/*. pause, resume and front change
docs/run-requests/QUEUE.json on test, and only after a person said yes (queue.py).
"""
import fnmatch
import json
import shutil
import subprocess
from pathlib import Path

from .common import QUEUE_FILE, REQUESTS, RunnerError, clean_env, run

RESULTS_PREFIX = "refs/heads/runs/"
MAX_FILE_BYTES = 5 * 1024 * 1024
MAX_REQUEST_BYTES = 50 * 1024 * 1024


def git(repo, *args, check=True):
    return run(["git", "-C", str(repo)] + list(args), check=check)


def fetch(repo, runs=False):
    refspecs = ["+refs/heads/test:refs/remotes/origin/test"]
    if runs:
        refspecs.append("+refs/heads/runs/*:refs/remotes/origin/runs/*")
    git(repo, "fetch", "--quiet", "origin", *refspecs)


def show(repo, ref, path):
    """A file's text at a ref, or None when it is not there."""
    done = run(["git", "-C", str(repo), "show", f"{ref}:{Path(path).as_posix()}"], check=False)
    return done if done else None


def exists(repo, spec):
    """Whether an object exists, such as <sha>^{commit} or <sha>:<path>."""
    return _ok(["git", "-C", str(repo), "cat-file", "-e", spec])


def _ok(args):
    return subprocess.run(args, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, env=clean_env()).returncode == 0


def is_ancestor(repo, sha, ref):
    return _ok(["git", "-C", str(repo), "merge-base", "--is-ancestor", sha, ref])


def run_json(repo, request_id, ref="origin/test"):
    return show(repo, ref, REQUESTS / request_id / "run.json")


def queue_text(repo, ref="origin/test"):
    return show(repo, ref, QUEUE_FILE)


def results_branches(repo):
    out = git(repo, "for-each-ref", "--format=%(refname:strip=3)", "refs/remotes/origin/runs/", check=False)
    return [line.split("/", 1)[1] for line in out.splitlines() if "/" in line]


def worktree(repo, work, sha):
    """A worktree at the request's commit, under <work>/<first 12 of the sha>, reused when it is there."""
    path = Path(work) / sha[:12]
    if path.exists():
        head = git(path, "rev-parse", "HEAD", check=False).strip()
        if head != sha:
            raise RunnerError(f"{path} is at {head[:12] or 'nothing'}, not {sha[:12]}: move it aside")
        return path
    if not exists(repo, f"{sha}^{{commit}}"):
        git(repo, "fetch", "--quiet", "origin", sha, check=False)
    path.parent.mkdir(parents=True, exist_ok=True)
    git(repo, "worktree", "add", "--detach", str(path), sha)
    return path


def publish(repo, work, request, out, extra_files=()):
    """Commit the results the request keeps to runs/<id> and push that branch, and only it."""
    branch = f"runs/{request.id}"
    ref = RESULTS_PREFIX + request.id
    if not ref.startswith(RESULTS_PREFIX) or ".." in request.id:
        raise RunnerError(f"refusing to push {ref}")
    path = Path(work) / "branches" / request.id
    if not path.exists():
        path.parent.mkdir(parents=True, exist_ok=True)
        git(repo, "fetch", "--quiet", "origin", f"+{ref}:refs/remotes/origin/{branch}", check=False)
        base = f"origin/{branch}" if _ok(["git", "-C", str(repo), "rev-parse", "--verify", "--quiet",
                                          f"refs/remotes/origin/{branch}"]) else request.commit
        git(repo, "worktree", "add", "-B", branch, str(path), base)
    dest = path / REQUESTS / request.id / "results"
    left = copy_results(Path(out), dest, request.keep, extra_files)
    git(path, "add", "-A", "--", str(dest.relative_to(path)))
    if not git(path, "status", "--porcelain", "--", str(dest.relative_to(path))).strip():
        return False
    counts = _counts(Path(out) / "manifest.json")
    git(path, "commit", "--quiet", "-m",
        f"Results of {request.id}: {counts} blocks done\n\nPushed by runq.py from the RTX 5080 machine's runner."
        + (f" {left} file(s) were too big and stay on the machine (results/LEFT_ON_MACHINE.md)." if left else ""))
    git(path, "push", "--quiet", "origin", f"HEAD:{ref}")
    return True


def _counts(manifest_path):
    try:
        data = json.loads(Path(manifest_path).read_text(encoding="utf-8"))
    except (OSError, ValueError):
        return "?"
    blocks = data.get("blocks", [])
    return f"{sum(1 for b in blocks if b.get('status') == 'done')} of {len(blocks)}"


def copy_results(out, dest, keep, extra_files=()):
    """Copy the manifest, the reports and what keep names; list what is too big. Returns how many were left."""
    if dest.exists():
        shutil.rmtree(str(dest))
    dest.mkdir(parents=True)
    chosen = [out / "manifest.json"] + sorted(out.glob("report-*.txt")) + [Path(p) for p in extra_files]
    for p in sorted(out.rglob("*")):
        rel = p.relative_to(out).as_posix()
        if p.is_file() and not rel.startswith("_aside/") and any(matches(rel, g) for g in keep):
            chosen.append(p)
    total, left, seen = 0, [], set()
    for p in chosen:
        if not p.is_file() or p in seen:
            continue
        seen.add(p)
        try:
            rel = p.relative_to(out)
        except ValueError:
            rel = Path(p.name)
        size = p.stat().st_size
        if size > MAX_FILE_BYTES or total + size > MAX_REQUEST_BYTES:
            left.append((rel.as_posix(), size))
            continue
        total += size
        target = dest / rel
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(str(p), str(target))
    if left:
        lines = ["# Left on the RTX 5080 machine", "",
                 f"Files over {MAX_FILE_BYTES // 2**20} MB each, or past {MAX_REQUEST_BYTES // 2**20} MB for the "
                 f"request, stay in the runner's results folder for {request_days()} days.", "",
                 "| File | Size |", "|---|---|"]
        lines += [f"| `{name}` | {size / 2**20:.1f} MB |" for name, size in left]
        (dest / "LEFT_ON_MACHINE.md").write_text("\n".join(lines) + "\n", encoding="utf-8")
    return len(left)


def request_days():
    return 30


def matches(rel, pattern):
    """A glob where **/ also matches no folder at all, so **/summary.md finds c1/summary.md and summary.md."""
    if fnmatch.fnmatch(rel, pattern):
        return True
    return pattern.startswith("**/") and fnmatch.fnmatch(rel, pattern[3:])
