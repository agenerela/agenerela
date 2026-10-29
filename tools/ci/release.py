"""Cut a release of the framework package: version bump, changelog, tag check, notes.

    python tools/ci/release.py prepare 0.2.0     bump the version and date the changelog
    python tools/ci/release.py check v0.2.0      is this tag ready to publish?
    python tools/ci/release.py notes v0.2.0      the release notes

docs/RELEASING.md is the whole process; this file does its mechanical parts. The release
workflow (.github/workflows/release.yml) runs check and notes when a v* tag is pushed, so
a tag that fails here never becomes a GitHub release. Standard library only, like every
other script under tools/.

The version lives in three places, and they must agree: package.json (what Unity's Package
Manager reads), AgenerelaInfo.Version (what decision telemetry is stamped with, so a
benchmark run can be traced to the framework that produced it), and the newest dated
section of the package's CHANGELOG.md. The hygiene check "version" keeps the first two
together on every pull request; this file moves all three at once.

The release notes are docs/releases/<tag>.md when it exists, written for people rather than
copied from the changelog, followed by the install line and a link to the changelog section.
Without that file they are the changelog section itself, as releases before 0.4.0 had.

A preview is a version with a pre-release suffix, such as 0.4.0-preview.1. It is published
as a GitHub pre-release, so the "latest release" badge and link stay on the last full one.
"""

import argparse
import datetime
import json
import os
import re
import subprocess
import sys

PACKAGE = "UnityProject/Packages/com.agenerela.framework"
MANIFEST = f"{PACKAGE}/package.json"
INFO = f"{PACKAGE}/Runtime/AgenerelaInfo.cs"
CHANGELOG = f"{PACKAGE}/CHANGELOG.md"
REPO = "https://github.com/agenerela/agenerela"
SEMVER = re.compile(r"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(-[0-9A-Za-z]+(\.[0-9A-Za-z]+)*)?$")
INFO_VERSION = re.compile(r'(public const string Version = ")([^"]*)(")')


def read(path):
    return open(path, encoding="utf-8").read()


def write(path, text):
    # Keep whatever line endings the checkout uses; git normalises them on commit.
    crlf = "\r\n" in open(path, encoding="utf-8", newline="").read()
    open(path, "w", encoding="utf-8", newline="\r\n" if crlf else "\n").write(text)


def manifest_version():
    return json.load(open(MANIFEST, encoding="utf-8"))["version"]


def info_version():
    m = INFO_VERSION.search(read(INFO))
    return m.group(2) if m else None


def section(version):
    """The body of the changelog's '## [version] - date' section, or None."""
    text = read(CHANGELOG)
    m = re.search(rf"^## \[{re.escape(version)}\] - (\d{{4}}-\d{{2}}-\d{{2}})[ \t]*\n(.*?)(?=^## |\Z)",
                  text, re.M | re.S)
    return (m.group(1), m.group(2).strip()) if m else None


def version_of(tag):
    if not tag.startswith("v") or not SEMVER.match(tag[1:]):
        sys.exit(f"error: {tag} is not a release tag. Tags are v<major>.<minor>.<patch>, such as v0.2.0, "
                 f"or a preview such as v0.4.0-preview.1.")
    return tag[1:]


def prepare(version, date):
    if not SEMVER.match(version):
        sys.exit(f"error: {version} is not a version. Use <major>.<minor>.<patch>, such as 0.2.0, "
                 f"or a preview such as 0.4.0-preview.1.")
    if section(version):
        sys.exit(f"error: CHANGELOG.md already has a section for {version}.")
    text = read(CHANGELOG)
    m = re.search(r"^## \[Unreleased\][ \t]*\n(.*?)(?=^## |\Z)", text, re.M | re.S)
    if not m or not m.group(1).strip():
        sys.exit("error: CHANGELOG.md has nothing under [Unreleased], so there is nothing to release.")
    # The released entries move under their version; a fresh, empty [Unreleased] stays on top.
    text = text[:m.start()] + f"## [Unreleased]\n\n## [{version}] - {date}\n\n{m.group(1).strip()}\n\n" + text[m.end():]
    write(CHANGELOG, text)

    write(MANIFEST, re.sub(r'("version":\s*")[^"]*(")', rf"\g<1>{version}\g<2>", read(MANIFEST), count=1))
    write(INFO, INFO_VERSION.sub(rf"\g<1>{version}\g<3>", read(INFO), count=1))
    print(f"Prepared {version}: package.json, AgenerelaInfo.Version and CHANGELOG.md ({date}).")
    print("Review the diff, then follow docs/RELEASING.md from step 2.")


def check(tag):
    version = version_of(tag)
    errors = []
    if manifest_version() != version:
        errors.append(f"package.json says {manifest_version()}, not {version}.")
    if info_version() != version:
        errors.append(f"AgenerelaInfo.Version says {info_version()}, not {version}.")
    if not section(version):
        errors.append(f"CHANGELOG.md has no '## [{version}] - <date>' section.")

    # Releases are cut from main, the branch only the repository owner can push to. A tag on
    # any other commit would publish work that never went through the test -> main merge.
    commit = subprocess.run(["git", "rev-parse", "--verify", "--quiet", f"{tag}^{{commit}}"],
                            capture_output=True, text=True).stdout.strip()
    if not commit:
        errors.append(f"There is no tag {tag} here. Create it on main first (docs/RELEASING.md).")
    elif subprocess.run(["git", "merge-base", "--is-ancestor", commit, "origin/main"]).returncode != 0:
        errors.append(f"{tag} points at {commit[:12]}, which is not on origin/main. Tag the commit main "
                      f"points at after the release is merged there.")

    for e in errors:
        print(f"error: {e}")
    if errors:
        return 1
    print(f"OK: {tag} is on main, and package.json, AgenerelaInfo.Version and CHANGELOG.md all say {version}.")
    return 0


def notes(tag):
    version = version_of(tag)
    found = section(version)
    if not found:
        sys.exit(f"error: CHANGELOG.md has no '## [{version}] - <date>' section.")
    date, body = found
    written = f"docs/releases/{tag}.md"
    if os.path.exists(written):
        anchor = re.sub(r"[^0-9a-z -]", "", f"{version} - {date}".lower()).replace(" ", "-")
        print(f"""{read(written).strip()}

## Install it

In Unity, open *Window → Package Manager → + → Install package from git URL* and paste:

```
{REPO}.git?path={PACKAGE}#{tag}
```

Newtonsoft JSON, the one thing it depends on, comes with it.

Every change in detail, for developers, is in the
[changelog]({REPO}/blob/{tag}/{CHANGELOG}#{anchor}). Found a problem, or something
confusing? [Open an issue]({REPO}/issues/new); even a one-liner helps.""")
        return 0
    print(f"""{body}

## Installing

In Unity, *Window → Package Manager → + → Install package from git URL*, then:

```
{REPO}.git?path={PACKAGE}#{tag}
```

The package's one dependency, Newtonsoft JSON, installs with it.""")
    return 0


def main():
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    sub = ap.add_subparsers(dest="command", required=True)
    p = sub.add_parser("prepare", help="bump the version everywhere and date the changelog")
    p.add_argument("version")
    p.add_argument("--date", default=datetime.date.today().isoformat(), help="release date (default: today)")
    sub.add_parser("check", help="is this tag ready to publish?").add_argument("tag")
    sub.add_parser("notes", help="print the release notes for this tag").add_argument("tag")
    args = ap.parse_args()

    root = subprocess.run(["git", "rev-parse", "--show-toplevel"], capture_output=True, text=True,
                          check=True).stdout.strip()
    os.chdir(root)

    if args.command == "prepare":
        prepare(args.version, args.date)
        return 0
    return check(args.tag) if args.command == "check" else notes(args.tag)


if __name__ == "__main__":
    sys.exit(main())
