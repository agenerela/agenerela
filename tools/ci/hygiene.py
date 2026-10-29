"""Repo hygiene checks: the same ones CI runs, runnable on your own machine.

    python tools/ci/hygiene.py              run every check
    python tools/ci/hygiene.py meta guids   run only these
    python tools/ci/hygiene.py --list       name every check
    python tools/ci/hygiene.py --install-hook

The workflow in .github/workflows/repo-hygiene.yml calls this file once per check, so a
check behaves the same on a runner and on a laptop. Standard library only, like every
other script under tools/.

None of this compiles C# or runs a Unity test: Unity in CI needs licence activation, which
the build plan defers to Phase 3 (section 6.3). What is here runs in seconds and catches the
mistakes that actually happen in a Unity repo: committed generated files, broken JSON,
missing .meta files, a .meta regenerated with a new GUID, a binary file that bypassed Git
LFS, a .gitignore rule that hides files the repo tracks, and scene code leaking into the
part of the framework an agent with no scene has to be able to run. It also holds the hard
rules that a machine can check (AGENTS.md): one dependency, no attribution lines, no key in
a URL - and the two checks section 6.3 asks for before Phase 3: doc links resolve, and the
Python probes lint.

The repo holds several Unity projects: UnityProject/, the framework's dev project, and one
per demo game under Demos/ (DR-010 in the build plan). Every check finds them all itself,
so a new game project is covered the moment its ProjectSettings/ is committed. Nothing here
needs editing when a game is added.

What a run compares against. Three checks ask what changed (guids, attribution, changelog)
and one words its advice by it (meta):
- On GitHub, a pull request counts from where it left its target branch, and a push from
  the commit the branch pointed at before. A new branch has nothing to count from.
- On your machine, it counts from where HEAD left origin/test (pass --base to choose),
  up to what is staged. So the pre-commit hook checks the commit you are about to make.
Every other check reads what git tracks, which locally means the index: a file you have
not staged is not checked.
"""

import argparse
import ast
import collections
import html
import json
import os
import posixpath
import re
import shlex
import subprocess
import sys
from urllib.parse import unquote

ON_GITHUB = os.environ.get("GITHUB_ACTIONS") == "true"
FRAMEWORK = "UnityProject/Packages/com.agenerela.framework"
HOOK_MARK = "# agenerela hygiene hook"


# ---------------------------------------------------------------------------- plumbing

def git(*args, stdin=None, check=True):
    return subprocess.run(["git", *args], input=stdin, capture_output=True, text=True,
                          encoding="utf-8", errors="replace", check=check).stdout


def tracked(*patterns):
    return [f for f in git("ls-files", "-z", "--", *patterns).split("\0") if f]


def folders_of(files):
    folders = set()
    for f in files:
        d = posixpath.dirname(f)
        while d and d not in folders:
            folders.add(d)
            d = posixpath.dirname(d)
    return folders


def error(msg, file=None, line=None):
    """A GitHub annotation on a runner; file:line: error: on a terminal."""
    if ON_GITHUB:
        where = ",".join(p for p in (f"file={file}" if file else "", f"line={line}" if line else "") if p)
        print(f"::error{' ' + where if where else ''}::{msg}")
    else:
        loc = f"{file}:{line}: " if file and line else f"{file}: " if file else ""
        print(f"{loc}error: {msg}")


def warning(msg):
    print(f"::warning::{msg}" if ON_GITHUB else f"warning: {msg}")


def projects():
    """A Unity project is any folder with a committed ProjectSettings/ProjectVersion.txt."""
    return [f[:-len("/ProjectSettings/ProjectVersion.txt")]
            for f in tracked("*/ProjectSettings/ProjectVersion.txt")]


def packages():
    """A package is any folder with a package.json outside a project's Assets/."""
    return [f[:-len("/package.json")] for f in tracked("*/package.json") if "/Assets/" not in f]


class Range:
    """What this run compares against; see the module docstring. head None is the index."""
    base = None
    head = None
    restore = "origin/test"
    pr_body = None
    pr_base = None
    pr_head = None


RANGE = Range()


def find_range(base_arg):
    r = RANGE
    if base_arg:
        r.base = git("rev-parse", "--verify", base_arg).strip()
        r.restore = base_arg
        return
    if ON_GITHUB:
        event = os.environ.get("GITHUB_EVENT_NAME")
        payload = json.load(open(os.environ["GITHUB_EVENT_PATH"], encoding="utf-8"))
        if event == "pull_request":
            pr = payload["pull_request"]
            r.pr_base, r.pr_head, r.pr_body = pr["base"]["sha"], pr["head"]["sha"], pr.get("body") or ""
            r.base = git("merge-base", r.pr_base, r.pr_head, check=False).strip() or None
            r.head = r.pr_head
            r.restore = "origin/" + os.environ["GITHUB_BASE_REF"]
        elif event == "push":
            before = payload.get("before") or ""
            if before.strip("0"):  # all zeros: the push created the branch
                # After a force-push the old commit is on no branch, so the checkout did not fetch it.
                exists = lambda: subprocess.run(["git", "cat-file", "-e", before + "^{commit}"],
                                                capture_output=True).returncode == 0
                if not exists():
                    subprocess.run(["git", "fetch", "--quiet", "--no-tags", "origin", before], capture_output=True)
                if exists():
                    r.base, r.head, r.restore = before, os.environ["GITHUB_SHA"], before[:12]
    else:
        if subprocess.run(["git", "rev-parse", "--verify", "--quiet", "origin/test"],
                          capture_output=True).returncode == 0:
            r.base = git("merge-base", "HEAD", "origin/test", check=False).strip() or None


def describe_range():
    if not RANGE.base:
        return "nothing (no earlier commit to compare with)"
    return f"{RANGE.base[:12]}..{RANGE.head[:12] if RANGE.head else 'the index'}"


def show(rev, path):
    """A file's content at a commit, or in the index when rev is None."""
    return git("show", f"{rev}:{path}" if rev else f":{path}")


def read(path, encoding="utf-8"):
    return open(path, encoding=encoding).read()


# ---------------------------------------------------------------------------- checks

def check_generated():
    """No generated or secret files committed."""
    fail = False

    def check(pattern, msg):
        nonlocal fail
        found = tracked(pattern)[:5]
        if found:
            error(msg)
            for f in found:
                print(f"    {f}")
            fail = True

    for p in projects():
        check(f"{p}/Library/**", f"{p}/Library/ is committed. It is generated by Unity and must never be tracked.")
        check(f"{p}/Logs/**", f"{p}/Logs/ is committed.")
        check(f"{p}/UserSettings/**", f"{p}/UserSettings/ is committed.")
        check(f"{p}/Temp/**", f"{p}/Temp/ is committed.")
    check("**/*.csproj", "A .csproj is committed. These are generated by Unity per-machine.")
    check("**/*.sln", "A .sln is committed.")
    check("**/*.slnx", "A .slnx is committed.")
    check(".env", "A .env file is committed. Rotate the keys in it immediately, then remove it from history.")
    if not fail:
        print("OK: no generated or secret files tracked")
    return not fail


def check_json():
    """JSON and asmdef files parse. A malformed settings or asmdef file fails silently in Unity."""
    fail = False
    for f in tracked("*.json", "*.asmdef", "*.asmref"):
        try:
            json.load(open(f, encoding="utf-8"))
        except (OSError, ValueError):
            error("Invalid JSON. A malformed settings or asmdef file fails silently in Unity.", file=f)
            fail = True
    if not fail:
        print("OK: all JSON and asmdef files parse")
    return not fail


def check_packages():
    """Package manifests are well formed."""
    fail = False
    for pkg in packages():
        p = f"{pkg}/package.json"
        try:
            d = json.load(open(p, encoding="utf-8"))
        except ValueError:
            fail = True
            continue  # the json check reports it
        missing = [k for k in ("name", "version", "displayName", "description", "unity") if not d.get(k)]
        if missing:
            error(f"Missing required field(s): {', '.join(missing)}", file=p)
            fail = True
        elif d["name"] != d["name"].lower():
            error("Package name must be lowercase (Unity requirement).", file=p)
            fail = True
        elif "unity" in d["name"]:
            error("Package name must not contain 'unity' - Unity forbids it.", file=p)
            fail = True
        else:
            print(f"OK: {d['name']} {d['version']}")
    return not fail


def check_test_asmdefs():
    """Test assemblies are excluded from player builds."""
    fail = False
    for f in tracked("*.asmdef"):
        if "Tests" not in f:
            continue
        d = json.load(open(f, encoding="utf-8"))
        if "UNITY_INCLUDE_TESTS" not in (d.get("defineConstraints") or []):
            error('Test assembly is missing defineConstraints ["UNITY_INCLUDE_TESTS"]. '
                  'Without it the assembly compiles into player builds and fails there.', file=f)
            fail = True
        elif "nunit.framework.dll" not in (d.get("precompiledReferences") or []):
            error('Test assembly is missing precompiledReferences ["nunit.framework.dll"].', file=f)
            fail = True
    if not fail:
        print("OK: test assemblies correctly constrained")
    return not fail


def check_meta():
    """Unity .meta files are in sync.

    A missing .meta means Unity regenerates it with a NEW guid on someone else's machine,
    silently breaking every reference to that asset. An orphaned .meta means a file was
    deleted without its metadata. Both are classic team bugs.

    Checked under every project's Assets/ and every package folder. Unity ignores hidden
    names and folders ending in ~ (Samples~, Documentation~), and so does this.

    The fix for a missing .meta depends on whether the asset is new. An existing one must
    get its committed .meta back: the copy Unity regenerates carries a new guid, which
    passes this check and breaks the same references (the guids check catches it).
    """
    all_tracked = tracked()
    files = set(all_tracked)
    folders = folders_of(all_tracked)

    roots = [p + "/Assets" for p in projects()] + packages()

    def ignored(rel):
        return any(part.startswith(".") or part.endswith("~") for part in rel.split("/"))

    base, restore = RANGE.base, RANGE.restore
    before = set(git("ls-tree", "-r", "--name-only", "-z", base).split("\0")) if base else set()

    restorable = []

    def remedy(meta):
        cmd = f"git checkout {restore} -- {shlex.quote(meta)}"
        if meta in before:
            restorable.append(meta)
            return (f"Do not commit the copy Unity regenerates: its guid is new, so the references break all "
                    f"the same. Restore the committed one from {restore}: {cmd}")
        if base:
            return "This is a new asset: open its project in Unity, then commit the .meta Unity creates for it."
        return (f"If this is a new asset, open its project in Unity, then commit the .meta Unity creates for it. "
                f"If it already existed, restore the committed .meta rather than a regenerated one: {cmd}")

    errors = []  # (message, file)
    need = set()
    for root in roots:
        for f in all_tracked:
            if not f.startswith(root + "/") or f.endswith(".meta"):
                continue
            rel = f[len(root) + 1:]
            if ignored(rel):
                continue
            if f + ".meta" not in files:
                errors.append((f"Missing {f}.meta - Unity will regenerate it with a new guid and "
                               f"break references. {remedy(f + '.meta')}", f))
            # Folders carry a .meta too, but git tracks only files. Every folder between
            # the root and the file needs one; the root itself has none.
            d = posixpath.dirname(rel)
            while d:
                need.add(f"{root}/{d}")
                d = posixpath.dirname(d)
    for d in sorted(need):
        if d + ".meta" not in files:
            errors.append((f"Missing {d}.meta - Unity will regenerate the folder's guid on every "
                           f"machine. {remedy(d + '.meta')}", None))

    for m in all_tracked:
        if not m.endswith(".meta"):
            continue
        asset = m[:-len(".meta")]
        if asset not in files and asset not in folders:
            errors.append(("Orphaned .meta - the asset it describes is not tracked.", m))
        elif not any(m.startswith(r + "/") for r in roots):
            errors.append(("This .meta is outside every Unity project and package. "
                           "If it belongs to a new project, commit that project's ProjectSettings/ too, "
                           "or none of its files are checked.", m))

    # GitHub shows a check's first ten annotations only, so the fix for them all goes first.
    if len(restorable) > 1:
        errors.insert(0, (f"{len(restorable)} committed .meta files are missing. Restore them all at once: "
                          f"git checkout {restore} -- {' '.join(shlex.quote(m) for m in restorable)}", None))
    for msg, f in errors:
        error(msg, file=f)
    if not errors:
        print("OK: .meta files in sync")
    return not errors


def check_guids():
    """Existing .meta files keep their GUIDs.

    The meta check asks whether every asset HAS a .meta, not whether it is the SAME one.
    Delete the metas, let Unity regenerate them and commit the copies, and that check passes
    while every scene, setting and asset that referred to the old GUIDs points at nothing:
    the render pipeline, the build's scene list, a scene's volume profile. Unity shows no
    error until someone opens the thing that broke, and every other check stays green - that
    happened to 40 metas in one pull request.

    So a tracked .meta whose guid changed fails, and so does an asset moved to another folder
    whose .meta Unity regenerated there (part 2 below). An asset moved together with its
    .meta keeps its guid and passes, and a new asset has no old guid to compare.
    """
    base, head, restore = RANGE.base, RANGE.head, RANGE.restore
    if not base:
        print("OK: no earlier commit to compare with, so no guid can have changed")
        return True
    print(f"Comparing {describe_range()}")
    to = [head] if head else []
    cached = [] if head else ["--cached"]

    def guid(rev, path):
        m = re.search(r"^guid:\s*(\w+)", show(rev, path), re.M)
        return m.group(1) if m else None

    # 1. A .meta rewritten in place. -G keeps the metas whose diff touches a guid line;
    # comparing the values then drops a line that was rewritten with the same guid.
    candidates = [f for f in git("diff", *cached, "--diff-filter=M", "-G^guid:", "--name-only", "-z",
                                 base, *to, "--", "*.meta").split("\0") if f]
    changed = [(f, guid(base, f), guid(head, f)) for f in candidates]
    changed = [c for c in changed if c[1] != c[2]]

    # 2. An asset moved to another folder with a .meta Unity regenerated there. Git records
    # that as a delete and an add, so part 1 never sees it. Git's rename detection is no help:
    # it pairs .meta files by their text, and two unrelated assets' .meta files are often
    # closer to each other than a regenerated one is to its original. So moves are paired
    # here from the assets themselves: a deleted file and an added file are the same asset
    # when they have the same file name AND the same content. Requiring both keeps a new
    # asset that merely shares a name from failing this check; the price is that a file moved
    # and edited in the same change is not checked. An identical file with the same name
    # cannot be told apart from a move, so asking it to keep its guid can only help.
    # A folder counts as moved when a file inside it moved to the same place inside a folder
    # of the same name. That is what moving a folder outside Unity does: its .meta sits
    # beside it, stays behind, and Unity makes a new one at the destination.
    deleted, added = {}, {}
    raw = git("diff", *cached, "--raw", "--no-renames", "--no-abbrev", "-z", base, *to).split("\0")
    for info, path in zip(raw[0::2], raw[1::2]):
        _, _, old_blob, new_blob, status = info.split()
        if status == "D":
            deleted[path] = old_blob
        elif status == "A":
            added[path] = new_blob

    same_asset = collections.defaultdict(lambda: ([], []))
    for side, files in ((0, deleted), (1, added)):
        for path, blob in files.items():
            if not path.endswith(".meta"):
                same_asset[(posixpath.basename(path), blob)][side].append(path)
    pairs = [(o, n) for olds, news in same_asset.values() for o in olds for n in news]
    for o, n in list(pairs):
        while True:
            o, n = posixpath.dirname(o), posixpath.dirname(n)
            if not o or not n or posixpath.basename(o) != posixpath.basename(n):
                break
            pairs.append((o, n))

    # Each added .meta, and the deleted ones it may have replaced. Several identical files
    # with the same name moving at once give several candidates; matching any one is a move.
    moves = collections.defaultdict(set)
    for o, n in pairs:
        if o + ".meta" in deleted and n + ".meta" in added:
            moves[n + ".meta"].add(o + ".meta")
    moved = []
    for new_meta, old_metas in sorted(moves.items()):
        olds = {m: guid(base, m) for m in sorted(old_metas)}
        new = guid(head, new_meta)
        if new not in olds.values():
            old_meta, old = next(iter(olds.items()))
            moved.append((old_meta, new_meta, old, new))

    if not changed and not moved:
        print(f"OK: every existing .meta keeps its guid ({len(candidates)} touched a guid line, "
              f"{len(moves)} moved with their asset)")
        return True

    # Summaries go first: GitHub shows only the first ten annotations of a step.
    if changed:
        everything = " ".join(shlex.quote(f) for f, _, _ in changed)
        error(f"{len(changed)} existing .meta file(s) changed their guid, which usually means they were "
              f"deleted and Unity regenerated them. Restore the committed ones: git checkout {restore} -- {everything}")
    if len(moved) > 1:
        error(f"{len(moved)} moved assets got a new .meta at their new path, and with it a new guid. "
              f"Each annotation below names the old .meta to put back.")
    for f, old, new in changed:
        error(f"guid changed from {old} to {new}. Unity finds an asset by its guid, so everything that referred "
              f"to this one now points at nothing, with no error until someone opens it. Restore the committed "
              f".meta: git checkout {restore} -- {shlex.quote(f)}", file=f)
    # Two commands, joined in prose: Windows PowerShell 5.1 has no &&, and its > would write
    # the .meta as UTF-16, so neither a && chain nor git show > file is safe to paste there.
    for old_meta, new_meta, old, new in moved:
        error(f"Moved from {old_meta}, but with a new .meta: guid changed from {old} to {new}. Unity finds an "
              f"asset by its guid, so everything that referred to it now points at nothing, with no error until "
              f"someone opens it. Put the old .meta at the new path: git checkout {restore} -- "
              f"{shlex.quote(old_meta)}, then git mv -f {shlex.quote(old_meta)} {shlex.quote(new_meta)}",
              file=new_meta)
    return False


def check_lfs():
    """Binary files go through Git LFS.

    LFS only intercepts a file whose pattern was in .gitattributes before it was committed.
    A binary type with no pattern goes into history as a plain blob, every clone downloads
    it forever, and lfs.fetchexclude (Demos/README.md) cannot skip it. Git's own idea of
    binary is enough here: an LFS pointer is text, so a binary blob in the index is a file
    that bypassed LFS.
    """
    binary = [line.split("\t", 1)[1] for line in git("ls-files", "--eol").splitlines()
              if line.startswith("i/-text")]
    for f in binary:
        ext = posixpath.splitext(f)[1] or posixpath.basename(f)
        error(f"A binary file outside Git LFS. Add a pattern for *{ext} to .gitattributes, under the "
              f"heading it belongs to, then move it into LFS: git add --renormalize {shlex.quote(f)}", file=f)
    if not binary:
        print("OK: every binary file goes through Git LFS")
    return not binary


def check_gitignore():
    """.gitignore ignores no tracked file.

    Git keeps tracking a file after a .gitignore rule starts to match it, so a bad rule
    changes nothing you can see. The damage is to NEW files: every one the rule matches is
    silently left out of commits. A "*.meta" rule drops the .meta of every asset created
    after it, and Unity gives each of those a new guid on every machine. The meta check
    would notice only later, one asset at a time.
    """
    hidden = [f for f in git("ls-files", "-ci", "--exclude-standard", "-z").split("\0") if f]
    if not hidden:
        print("OK: .gitignore ignores no tracked file")
        return True

    # Name the rule responsible. --no-index, or check-ignore skips files that are tracked;
    # it exits 1 when it matches nothing, which the fallback below covers.
    out = git("check-ignore", "-v", "--no-index", "-z", "--stdin", stdin="\0".join(hidden) + "\0",
              check=False).split("\0")
    rules = collections.defaultdict(list)
    for source, line, pattern, path in zip(*[iter(out)] * 4):
        rules[(source, line, pattern)].append(path)
    unexplained = sorted(set(hidden) - {p for paths in rules.values() for p in paths})
    if unexplained:
        rules[("", "", "")] = unexplained

    for (source, line, pattern), paths in rules.items():
        rule = f'The rule "{pattern}"' if pattern else "A .gitignore rule"
        error(f"{rule} ignores {len(paths)} tracked file(s), such as {paths[0]}. Git keeps tracking those, "
              f"so nothing looks wrong, but every new file the rule matches is silently left out of commits. "
              f"Remove the rule; or, if the files really should not be tracked, untrack them with "
              f"git rm --cached <path>.", file=source or None, line=line or None)
        for p in paths[:20]:
            print(f"    {p}")
        if len(paths) > 20:
            print(f"    ... and {len(paths) - 20} more")
    return False


def check_unity_version():
    """Every Unity project uses the pinned Unity version.

    One editor version for every project (build plan section 6.2). Opening a project in a
    newer patch rewrites ProjectVersion.txt and can silently upgrade serialized assets.
    """
    def version(p):
        m = re.search(r"^m_EditorVersion:\s*(\S+)", read(f"{p}/ProjectSettings/ProjectVersion.txt"), re.M)
        return m.group(1) if m else None

    pinned = version("UnityProject")
    fail = False
    for p in projects():
        v = version(p)
        if v != pinned:
            error(f"{p} is on Unity {v}, but UnityProject/ pins {pinned}. Every project must use the same version.",
                  file=f"{p}/ProjectSettings/ProjectVersion.txt")
            fail = True
    if not fail:
        print(f"OK: every Unity project is on {pinned}")
    return not fail


def check_version():
    """The framework's version agrees with itself.

    package.json is what Unity's Package Manager reads; AgenerelaInfo.Version is what every
    decision's telemetry is stamped with, so a benchmark run can be traced to the framework
    that produced it. A release changes both, and the changelog, at once (tools/ci/release.py
    prepare); one changed by hand drifts from the others with no compile error. Between
    releases the version stays at the last one, so it always has a dated changelog section.
    """
    manifest = json.load(open(f"{FRAMEWORK}/package.json", encoding="utf-8"))["version"]
    info_path = f"{FRAMEWORK}/Runtime/AgenerelaInfo.cs"
    m = re.search(r'public const string Version = "([^"]*)"', read(info_path))
    info = m.group(1) if m else None
    changelog = f"{FRAMEWORK}/CHANGELOG.md"
    fail = False
    if info != manifest:
        error(f"AgenerelaInfo.Version is {info}, but package.json says {manifest}. Change the version with "
              f"python tools/ci/release.py prepare <version>, which moves both and the changelog together.",
              file=info_path)
        fail = True
    if not re.search(rf"^## \[{re.escape(manifest)}\] - \d{{4}}-\d{{2}}-\d{{2}}", read(changelog), re.M):
        error(f"package.json says {manifest}, but CHANGELOG.md has no '## [{manifest}] - <date>' section. "
              f"Between releases the version stays at the last release; docs/RELEASING.md says how to cut one.",
              file=changelog)
        fail = True
    if not fail:
        print(f"OK: package.json, AgenerelaInfo.Version and CHANGELOG.md agree on {manifest}")
    return not fail


def check_framework_ref():
    """Demo projects load the framework from this repo.

    Every project other than UnityProject/ is a demo game (DR-010). It must load the
    framework through a relative file: path, so it always compiles against the code in this
    commit. A version or git URL pins a copy, and the game quietly stops testing the
    framework; an absolute path exists on one machine only.
    """
    demos = [p for p in projects() if p != "UnityProject"]
    if not demos:
        print("OK: no demo game projects yet")
    fail = False
    for p in demos:
        manifest = f"{p}/Packages/manifest.json"
        expected = "file:" + posixpath.relpath(FRAMEWORK, f"{p}/Packages")
        try:
            ref = json.load(open(manifest, encoding="utf-8")).get("dependencies", {}).get("com.agenerela.framework")
        except (OSError, ValueError) as e:
            error(f"Cannot read the project manifest: {e}", file=manifest)
            fail = True
            continue
        path = ref[len("file:"):] if ref and ref.startswith("file:") else None
        if ref is None:
            why = "does not reference the framework"
        elif path is None:
            why = f'loads the framework from "{ref}", which pins a copy instead of using the code in this repo'
        elif path.startswith("/") or re.match(r"[A-Za-z]:", path):
            why = f'uses the absolute path "{path}", which exists on one machine only'
        elif posixpath.normpath(posixpath.join(p, "Packages", path)) != FRAMEWORK:
            why = f'points at "{path}", which is not the framework in this repo'
        else:
            print(f"OK: {p} loads the framework from {ref}")
            continue
        error(f'{p} {why}. Use "com.agenerela.framework": "{expected}"', file=manifest)
        fail = True
    return not fail


def _code_only(src):
    """Blank out comments, strings and char literals, keeping every newline so line numbers
    still match. Verbatim strings may span lines; regular ones may not."""
    out, i, n = [], 0, len(src)
    blank = lambda t: re.sub(r"[^\n]", " ", t)
    while i < n:
        c, nx = src[i], src[i + 1:i + 2]
        if c == "/" and nx == "/":
            j = src.find("\n", i)
            j = n if j < 0 else j
        elif c == "/" and nx == "*":
            j = src.find("*/", i + 2)
            j = n if j < 0 else j + 2
        elif c in "@$" and re.match(r'(@\$?|\$@)"', src[i:i + 3]):
            j = src.index('"', i) + 1
            while j < n:
                if src[j] == '"' and src[j + 1:j + 2] == '"':
                    j += 2
                elif src[j] == '"':
                    j += 1
                    break
                else:
                    j += 1
        elif c == '"' or (c == "$" and nx == '"'):
            j = i + (2 if c == "$" else 1)
            while j < n and src[j] not in '"\n':
                j += 2 if src[j] == "\\" else 1
            j += 1
        elif c == "'":
            j = i + 1
            j += 2 if src[j:j + 1] == "\\" else 1
            j = src.find("'", j) + 1 or n
        else:
            out.append(c)
            i += 1
            continue
        out.append(blank(src[i:j]))
        i = j
    return "".join(out)


def _editor_guarded_lines(code):
    """Which lines sit inside a branch that only compiles in the editor. Handles
    #if UNITY_EDITOR / #if !UNITY_EDITOR with #elif, #else and nesting; any other
    condition counts as not guarded."""
    guarded, stack = set(), []
    for no, line in enumerate(code.split("\n"), 1):
        d = line.strip()
        m = re.match(r"#\s*(if|elif|else|endif)\b(.*)", d)
        if m:
            kind, cond = m.group(1), m.group(2)
            if kind == "if":
                stack.append(["pos" if re.search(r"(?<!!)\bUNITY_EDITOR\b", cond) and "!" not in cond
                              else "neg" if re.search(r"!\s*UNITY_EDITOR\b", cond) else "other", "if"])
            elif kind == "elif" and stack:
                stack[-1] = ["pos" if re.search(r"(?<!!)\bUNITY_EDITOR\b", cond) and "!" not in cond
                             else "other", "if"]
            elif kind == "else" and stack:
                stack[-1][1] = "else"
            elif kind == "endif" and stack:
                stack.pop()
            continue
        if any((k == "pos" and b == "if") or (k == "neg" and b == "else") for k, b in stack):
            guarded.add(no)
    return guarded


def check_scene():
    """Only Runtime/Unity/ touches a scene.

    An agent must be able to exist with no scene: a country in GreyBoxStrategy has no
    Transform, and an EditMode test has no scene at all (DR-011, build plan section 1.2).
    So scene types and calls may appear only under Runtime/Unity/. UnityEngine itself is
    fine - Awaitable, ScriptableObject and [Tooltip] need no scene.

    Also: UnityEditor anywhere in Runtime/ must sit inside #if UNITY_EDITOR. Unguarded, it
    compiles in the editor and breaks every player build, and nothing else here would
    notice, because CI does not compile Unity yet (section 6.3).

    Comments and string literals are ignored, so a doc comment may still say GameObject.
    """
    runtime = FRAMEWORK + "/Runtime/"
    scene_ok = runtime + "Unity/"
    scene = re.compile(
        r"\b(GameObject|MonoBehaviour|Component|Transform|RectTransform|Camera"
        r"|SceneManager|SceneManagement|Physics|Physics2D"
        r"|GetComponents?(?:InChildren|InParent)?|AddComponent"
        r"|Find(?:Any|First)?Objects?(?:Of|By)Type|FindWithTag|FindGameObjectsWithTag)\b")
    editor = re.compile(r"\bUnityEditor\b")

    files = tracked(runtime + "*.cs")
    fail = False
    for f in files:
        code = _code_only(read(f, "utf-8-sig"))
        safe = _editor_guarded_lines(code)
        for no, line in enumerate(code.split("\n"), 1):
            if not f.startswith(scene_ok):
                for m in scene.finditer(line):
                    error(f"{m.group(1)} is a scene type or call, and only Runtime/Unity/ may touch a scene "
                          f"(DR-011, build plan section 1.2). Move this code there, or rename it if it is not "
                          f"the Unity type.", file=f, line=no)
                    fail = True
            if no not in safe:
                for m in editor.finditer(line):
                    error("UnityEditor outside #if UNITY_EDITOR. It compiles in the editor and breaks every "
                          "player build (build plan section 1.2).", file=f, line=no)
                    fail = True
    if not fail:
        print(f"OK: {len(files)} runtime files, scene types only under Runtime/Unity/")
    return not fail


def check_dependency():
    """The framework depends on nothing beyond Newtonsoft.

    Hard rule 6 and DR-009: installing the framework must never pull in a scoped registry
    or native binaries. package.json may declare only Newtonsoft, and every framework
    assembly must set overrideReferences and name only Newtonsoft.Json.dll - otherwise a DLL
    that merely exists in this project compiles here and fails on a user's machine. The
    plan's own exception is honoured: a provider assembly that compiles only when its
    dependency is present (a versionDefine used in its defineConstraints, build plan
    section 1.2) is exempt.
    """
    allowed_packages = {"com.unity.nuget.newtonsoft-json"}
    allowed_dlls = {"Newtonsoft.Json.dll"}
    why = ("Newtonsoft is the core's one third-party dependency; another needs a decision record first "
           "(hard rule 6, DR-009).")
    errors = []

    manifest = json.load(open(f"{FRAMEWORK}/package.json", encoding="utf-8"))
    for dep in sorted(set(manifest.get("dependencies") or {}) - allowed_packages):
        errors.append((f"The framework depends on {dep}. {why}", f"{FRAMEWORK}/package.json"))

    asmdefs = tracked(f"{FRAMEWORK}/*.asmdef")
    own = set()
    for f in asmdefs:
        own.add(json.load(open(f, encoding="utf-8-sig"))["name"])
        try:
            guid = re.search(r"^guid:\s*(\w+)", read(f + ".meta"), re.M)
            if guid:
                own.add("GUID:" + guid.group(1))
        except OSError:
            pass  # the meta check reports a missing .meta

    for f in asmdefs:
        if "/Tests/" in f:
            continue  # editor-only; the test-asmdefs check covers these
        a = json.load(open(f, encoding="utf-8-sig"))
        # The rule's own exception: a provider that compiles only when its dependency is present.
        defines = {v.get("define") for v in a.get("versionDefines") or []}
        if defines & set(a.get("defineConstraints") or []):
            print(f"OK: {f} is isolated behind a version define")
            continue
        if not a.get("overrideReferences"):
            errors.append(("overrideReferences must be true. Without it Unity references every DLL in a user's "
                           "project, and an undeclared dependency compiles here and fails there (DR-009).", f))
        for dll in sorted(set(a.get("precompiledReferences") or []) - allowed_dlls):
            errors.append((f"References {dll}. {why}", f))
        for ref in a.get("references") or []:
            if ref not in own:
                errors.append((f"References the assembly {ref}, which is not part of the framework. If it is a "
                               f"provider's dependency, isolate that provider behind a versionDefine used in its "
                               f"defineConstraints (build plan section 1.2); otherwise: {why}", f))
    for msg, f in errors:
        error(msg, file=f)
    if not errors:
        print(f"OK: {len(asmdefs)} framework assemblies depend on nothing beyond Newtonsoft")
    return not errors


def check_links():
    """Links between docs resolve.

    Section 6.3: a markdown link check from Phase 0. Covers every relative link in a tracked
    .md file and every relative href/src in docs/*.html, resolved against what git tracks -
    a link to an ignored or untracked file is broken on GitHub even if it opens on your
    machine. External URLs and #anchors are not followed.
    """
    all_tracked = tracked()
    files = set(all_tracked)
    folders = folders_of(all_tracked)

    def targets_md(text):
        # Links inside fenced blocks and inline code are examples, not links.
        text = re.sub(r"^(```|~~~).*?^\1", lambda m: "\n" * m.group(0).count("\n"), text, flags=re.S | re.M)
        text = re.sub(r"`[^`\n]*`", "", text)
        for m in re.finditer(r"\]\(\s*<?([^)\s>]+)>?(?:\s+\"[^\"]*\")?\s*\)", text):
            yield text.count("\n", 0, m.start()) + 1, m.group(1)
        for m in re.finditer(r"^\s{0,3}\[[^\]]+\]:\s*<?(\S+?)>?(?:\s|$)", text, re.M):
            yield text.count("\n", 0, m.start()) + 1, m.group(1)

    def targets_html(text):
        text = re.sub(r"<(script|style|pre|code)\b.*?</\1>", lambda m: "\n" * m.group(0).count("\n"),
                      text, flags=re.S | re.I)
        for m in re.finditer(r"\b(?:href|src)\s*=\s*([\"'])(.*?)\1", text, re.I):
            yield text.count("\n", 0, m.start()) + 1, html.unescape(m.group(2))

    fail, checked = False, 0
    for f in all_tracked:
        if f.endswith(".md"):
            parse = targets_md
        elif f.endswith(".html") and f.startswith("docs/"):
            parse = targets_html
        else:
            continue
        try:
            found = parse(read(f))
        except OSError:
            continue  # deleted from the working tree but still staged
        for line, raw in found:
            t = raw.strip()
            if not t or t.startswith(("#", "mailto:", "data:", "javascript:")) or re.match(r"^[a-z][a-z0-9+.-]*://", t, re.I):
                continue
            path = unquote(t.split("#", 1)[0].split("?", 1)[0])
            if not path:
                continue
            resolved = posixpath.normpath(path.lstrip("/") if path.startswith("/")
                                          else posixpath.join(posixpath.dirname(f), path))
            checked += 1
            if resolved.rstrip("/") not in files and resolved.rstrip("/") not in folders:
                error(f"Broken link: {raw} (resolves to {resolved}, which is not a tracked file or folder).",
                      file=f, line=line)
                fail = True
    if not fail:
        print(f"OK: {checked} relative links resolve")
    return not fail


def check_probes():
    """Python probes lint, and no API key sits in a URL.

    Section 6.3 asks for probe linting before Phase 3. The scripts under tools/ must compile
    and import only the standard library, as their READMEs promise - this file included. A
    file that imports bpy is also a Blender script (tools/comfyui/preview_glb.py): the
    modules Blender itself provides are allowed there, because that part only runs inside
    Blender and the plain-Python part still needs no pip install. And hard rule 1 - keys go
    in headers - is checked across every .py and .cs file.

    One folder may import more, named module by module below: tools/branding/ holds scripts
    a person runs by hand to make the logo's prints, stills, animation and sound, and its
    README lists what they need. They are not probes, nothing imports them, and they must
    still compile. A module missing from the list still fails, so a new dependency there is
    a deliberate change to this file and to that README.
    """
    extra_imports = {
        "tools/branding/": {"numpy", "scipy"},  # sfx.py, the logo animation's sound
    }
    errors = []
    probes = tracked("tools/*.py")
    stdlib = set(sys.stdlib_module_names)
    for f in probes:
        src = read(f)
        try:
            tree = compile(src, f, "exec", ast.PyCF_ONLY_AST)
            compile(tree, f, "exec")
        except SyntaxError as e:
            errors.append((f"Does not compile: {e.msg}", f, e.lineno))
            continue
        siblings = {posixpath.splitext(posixpath.basename(p))[0] for p in probes
                    if posixpath.dirname(p) == posixpath.dirname(f)}
        imports = []
        for node in ast.walk(tree):
            if isinstance(node, ast.Import):
                imports += [(node.lineno, a.name) for a in node.names]
            elif isinstance(node, ast.ImportFrom) and node.level == 0 and node.module:
                imports.append((node.lineno, node.module))
        tops = {mod.split(".")[0] for _, mod in imports}
        blender = {"bpy", "bmesh", "bpy_extras", "mathutils"} if "bpy" in tops else set()
        extra = set().union(*(mods for folder, mods in extra_imports.items() if f.startswith(folder)))
        for lineno, mod in imports:
            if mod.split(".")[0] not in stdlib | siblings | blender | extra:
                errors.append((f"Imports {mod}, which is not in the standard library. The probes promise to run "
                               f"with no pip install (tools/benchmarks/README.md).", f, lineno))

    # Hard rule 1: keys travel in headers. A key query parameter is how Gemini's own examples
    # do it, which is exactly why it is worth a check - the key then lands in logs, proxies
    # and history. The pattern is split in two so this file does not match itself.
    key_in_url = re.compile(r"[?&](?:api_?)?" + r"key=", re.I)
    for f in tracked("*.py", "*.cs"):
        for no, line in enumerate(open(f, encoding="utf-8-sig", errors="replace"), 1):
            if key_in_url.search(line):
                errors.append(("An API key in a URL. Send it in a header and redact it from errors (hard rule 1); "
                               "a key in a URL ends up in logs, proxies and history.", f, no))
    for msg, f, line in errors:
        error(msg, file=f, line=line)
    if not errors:
        print(f"OK: {len(probes)} probes compile on the standard library alone; no key in any URL")
    return not errors


def check_attribution():
    """No attribution lines in commits or the pull request description.

    Hard rule 7: no Co-Authored-By, no "Generated with <tool>". Claude Code's settings strip
    them already; every other agent and every human follows the rule only by convention. On
    GitHub this reads every commit in the pull request and its description, which arrives
    through the event file, never pasted into a script, so it cannot inject anything. On
    your machine it reads the commits on your branch since it left origin/test.
    """
    tools = "claude|codex|copilot|cursor|chatgpt|gpt|gemini|aider|windsurf|devin|zed|openai|anthropic"
    trailer = re.compile(r"^\s*(?:co-authored-by\s*:|\W{0,3}\s*generated\s+(?:with|by)\s+\[?(?:" + tools + r")\b)",
                         re.I | re.M)
    if RANGE.pr_base:
        log_range = f"{RANGE.pr_base}..{RANGE.pr_head}"
    elif RANGE.base:
        log_range = f"{RANGE.base}..HEAD"
    else:
        print("OK: no earlier commit to compare with, so no commits to read")
        return True
    errors = []
    log = git("log", "--format=%h%x00%B%x01", log_range)
    commits = [c.strip("\n") for c in log.split("\x01") if c.strip()]
    for c in commits:
        sha, _, body = c.partition("\x00")
        for m in trailer.finditer(body):
            errors.append(f'Commit {sha} carries an attribution line: "{m.group(0).strip()}". Hard rule 7 - the '
                          f"history reads as the team's work. Reword the commit and force-push the branch.")
    for m in trailer.finditer(RANGE.pr_body or ""):
        errors.append(f'The pull request description carries an attribution line: "{m.group(0).strip()}". '
                      f"Hard rule 7 - edit it out of the description, then re-run this check.")
    for msg in errors:
        error(msg)
    if not errors:
        what = "commits and the description" if RANGE.pr_base else "commits"
        print(f"OK: {len(commits)} {what} carry no attribution lines")
    return not errors


CHANGELOGS = ("CHANGELOG.md", f"{FRAMEWORK}/CHANGELOG.md")
NO_CHANGELOG = re.compile(r"^\s*No changelog:\s*\S", re.I | re.M)


def check_changelog():
    """A change outside the docs comes with a changelog entry (a reminder; it never fails).

    Each pull request writes its own entry under [Unreleased]: in the package's CHANGELOG.md
    for a change to its public API, in the root one for everything else (AGENTS.md, "Where
    knowledge lives"). This replaced a status page that was updated after every merge, and
    fell behind whenever nobody did. A pull request that touches only Markdown and docs/
    needs no entry. For any other, a missing entry is a warning on the pull request, never a
    failure: the entry is a rule, but writing a good one takes a teammate without an agent
    longer than a red check should wait, and the reviewer asks for it (REVIEWING.md). A
    line "No changelog: <reason>" in the description silences the warning. The pre-commit
    hook skips this check, because a feature usually spans several commits.
    """
    if not RANGE.base:
        print("OK: no earlier commit to compare with, so no change to need an entry")
        return True
    to = [RANGE.head] if RANGE.head else []
    cached = [] if RANGE.head else ["--cached"]
    changed = [f for f in git("diff", *cached, "--name-only", "--no-renames", "-z", RANGE.base, *to).split("\0") if f]
    needs = [f for f in changed if not (f.startswith("docs/") or f.endswith(".md"))]
    if not needs:
        print(f"OK: {len(changed)} changed file(s), all docs, so no entry is needed")
        return True
    if any(f in changed for f in CHANGELOGS):
        print(f"OK: {len(needs)} changed file(s) outside the docs, and a changelog entry with them")
        return True
    if NO_CHANGELOG.search(RANGE.pr_body or ""):
        print(f"OK: {len(needs)} changed file(s) outside the docs; the description says why no entry is needed")
        return True
    msg = (f"{len(needs)} file(s) outside the docs changed (first: {needs[0]}), but neither CHANGELOG.md nor "
           f"{FRAMEWORK}/CHANGELOG.md did. Add an entry under [Unreleased] in the package's for a public API "
           f"change and the root one otherwise, or put a line 'No changelog: <reason>' in the description.")
    warning(msg)
    return True


CHECKS = {
    "generated": check_generated,
    "json": check_json,
    "packages": check_packages,
    "test-asmdefs": check_test_asmdefs,
    "meta": check_meta,
    "guids": check_guids,
    "lfs": check_lfs,
    "gitignore": check_gitignore,
    "unity-version": check_unity_version,
    "version": check_version,
    "framework-ref": check_framework_ref,
    "scene": check_scene,
    "dependency": check_dependency,
    "links": check_links,
    "probes": check_probes,
    "attribution": check_attribution,
    "changelog": check_changelog,
}


# ---------------------------------------------------------------------------- hook

HOOK = f"""#!/bin/sh
{HOOK_MARK}
# Runs the repo hygiene checks on what is about to be committed. Installed by
# python tools/ci/hygiene.py --install-hook; delete this file to remove it.
# Skip it once with git commit --no-verify.
if command -v python3 >/dev/null 2>&1; then py=python3; else py=python; fi
exec "$py" tools/ci/hygiene.py --hook
"""


def install_hook():
    # Not core.hooksPath: Git LFS keeps its own hooks in .git/hooks (pre-push, post-checkout,
    # post-commit, post-merge), and pointing hooksPath anywhere else silently switches those
    # off, so pushes would stop uploading LFS files. pre-commit is one LFS never uses.
    hooks = git("rev-parse", "--git-path", "hooks").strip()
    path = os.path.join(hooks, "pre-commit")
    if os.path.exists(path) and HOOK_MARK not in read(path):
        print(f"error: {path} already exists and is not this one. Merge them by hand, or move it aside first.")
        return False
    os.makedirs(hooks, exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(HOOK)
    os.chmod(path, 0o755)
    print(f"Installed {path}. Every commit now runs the hygiene checks first; git commit --no-verify skips them.")
    return True


# ---------------------------------------------------------------------------- main

def main():
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    ap.add_argument("checks", nargs="*", metavar="check", help="checks to run (default: all); see --list")
    ap.add_argument("--list", action="store_true", help="name every check and stop")
    ap.add_argument("--base", help="compare against this commit instead of where HEAD left origin/test")
    ap.add_argument("--install-hook", action="store_true", help="run these checks before every commit")
    ap.add_argument("--hook", action="store_true", help=argparse.SUPPRESS)
    args = ap.parse_args()

    os.chdir(git("rev-parse", "--show-toplevel").strip())

    if args.list:
        for name, fn in CHECKS.items():
            print(f"{name:14} {fn.__doc__.strip().splitlines()[0]}")
        return 0
    if args.install_hook:
        return 0 if install_hook() else 1

    unknown = [c for c in args.checks if c not in CHECKS]
    if unknown:
        ap.error(f"unknown check(s): {', '.join(unknown)}. --list names them.")
    names = args.checks or list(CHECKS)
    if args.hook:
        # The commit message does not exist yet, and a feature's changelog entry may land in a
        # later commit than its code; the pull request checks both.
        names = [n for n in names if n not in ("attribution", "changelog")]

    find_range(args.base)
    if not RANGE.base and "guids" in names:
        warning("No earlier commit to compare with, so the guids check has nothing to check.")

    if len(names) == 1:
        return 0 if CHECKS[names[0]]() else 1

    failed = []
    for name in names:
        print(f"\n== {name}: {CHECKS[name].__doc__.strip().splitlines()[0]}")
        if not CHECKS[name]():
            failed.append(name)
    print()
    if failed:
        print(f"FAILED: {', '.join(failed)}")
        if args.hook:
            print("The commit was stopped. Fix the above, or skip once with git commit --no-verify.")
        return 1
    print(f"All {len(names)} checks passed.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
