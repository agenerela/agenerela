"""Download the model files listed in models.json into ComfyUI's models folder.

Runs inside the container, which mounts this file and models.json:

    docker compose run --rm comfyui python /opt/agenerela/download_models.py          # every group
    docker compose run --rm comfyui python /opt/agenerela/download_models.py sfx      # one group
    docker compose run --rm comfyui python /opt/agenerela/download_models.py --list

"Every group" leaves out groups marked "optional" in models.json, such as the models of
experimental workflows; name one to fetch it.

Safe to re-run. A file already present at the right size is skipped, and an
interrupted download resumes where it stopped. Each file is checked against its
SHA-256 before it gets its final name, so ComfyUI never sees a truncated model.

Every file in models.json is ungated. For a gated model added later, accept its
licence on Hugging Face and pass a token: docker compose run --rm -e HF_TOKEN=...
"""

import argparse
import hashlib
import json
import os
import shutil
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path

MANIFEST = Path(__file__).with_name("models.json")
MODELS_DIR = Path(os.environ.get("COMFY_MODELS_DIR", "/data/models"))
CHUNK = 8 * 1024 * 1024


class DropAuthOnRedirect(urllib.request.HTTPRedirectHandler):
    """Hugging Face redirects each download to a CDN host; a token must not follow it there."""

    def redirect_request(self, req, fp, code, msg, headers, newurl):
        new = super().redirect_request(req, fp, code, msg, headers, newurl)
        if new is not None and urllib.parse.urlsplit(newurl).hostname != urllib.parse.urlsplit(req.full_url).hostname:
            new.remove_header("Authorization")
        return new


def gb(n):
    return f"{n / 1e9:.2f} GB"


def sha256_of(path):
    digest = hashlib.sha256()
    with open(path, "rb") as fh:
        while block := fh.read(CHUNK):
            digest.update(block)
    return digest.hexdigest()


def fetch(entry, part, opener, token):
    have = part.stat().st_size if part.exists() else 0
    if have > entry["size"]:
        part.unlink()
        have = 0
    if have == entry["size"]:
        return
    req = urllib.request.Request(entry["url"], headers={"User-Agent": "agenerela-comfyui-downloader"})
    if token and urllib.parse.urlsplit(entry["url"]).hostname == "huggingface.co":
        req.add_header("Authorization", "Bearer " + token)
    if have:
        req.add_header("Range", f"bytes={have}-")
    try:
        resp = opener.open(req, timeout=60)
    except urllib.error.HTTPError as e:
        if e.code in (401, 403):
            raise SystemExit(f"{entry['file']}: HTTP {e.code}. The model is gated: accept its licence on "
                             "Hugging Face, then pass -e HF_TOKEN=<your token>.") from None
        raise SystemExit(f"{entry['file']}: HTTP {e.code} from {entry['url']}. Has the file moved? "
                         "Update models.json.") from None
    with resp:
        if have and resp.status != 206:
            have = 0  # the server ignored the range request: start over
        if have:
            print(f"  resuming at {gb(have)}")
        done, started, last = have, time.monotonic(), 0.0
        with open(part, "ab" if have else "wb") as fh:
            while block := resp.read(CHUNK):
                fh.write(block)
                done += len(block)
                now = time.monotonic()
                if now - last >= 10:
                    rate = (done - have) / max(now - started, 1e-6) / 1e6
                    print(f"  {100 * done / entry['size']:5.1f}%  {gb(done)} of {gb(entry['size'])}  {rate:.0f} MB/s", flush=True)
                    last = now


def download(entry, opener, token):
    target = MODELS_DIR / entry["dir"] / entry["file"]
    label = f"{entry['dir']}/{entry['file']}"
    if target.exists() and target.stat().st_size == entry["size"]:
        print(f"ok        {label}")
        return
    print(f"download  {label} ({gb(entry['size'])})", flush=True)
    target.parent.mkdir(parents=True, exist_ok=True)
    part = target.with_name(target.name + ".part")
    fetch(entry, part, opener, token)
    if part.stat().st_size != entry["size"]:
        raise SystemExit(f"{label}: got {part.stat().st_size} bytes, expected {entry['size']}. Re-run to resume.")
    print("  verifying SHA-256", flush=True)
    if sha256_of(part) != entry["sha256"]:
        part.unlink()
        raise SystemExit(f"{label}: checksum mismatch; the partial file was deleted. Re-run to download it again.")
    os.replace(part, target)
    print(f"ok        {label}")


def main():
    groups = json.loads(MANIFEST.read_text(encoding="utf-8"))["groups"]
    parser = argparse.ArgumentParser(description="Download the models the workflows need.")
    parser.add_argument("groups", nargs="*", metavar="GROUP",
                        help=f"any of: {', '.join(groups)} (default: every group not marked optional)")
    parser.add_argument("--list", action="store_true", help="show the groups and their sizes, download nothing")
    args = parser.parse_args()

    unknown = [g for g in args.groups if g not in groups]
    if unknown:
        parser.error(f"unknown group(s) {', '.join(unknown)}; choose from {', '.join(groups)}")
    chosen = args.groups or [name for name, group in groups.items() if not group.get("optional")]

    if args.list:
        for name, group in groups.items():
            note = "  (optional: only when named)" if group.get("optional") else ""
            print(f"{name:<14} {gb(sum(f['size'] for f in group['files'])):>9}  {group['description']}{note}")
        return

    entries = [f for g in chosen for f in groups[g]["files"]]
    missing = [f for f in entries if not ((MODELS_DIR / f["dir"] / f["file"]).exists())]
    need = sum(f["size"] for f in missing)
    MODELS_DIR.mkdir(parents=True, exist_ok=True)
    free = shutil.disk_usage(MODELS_DIR).free
    print(f"groups: {', '.join(chosen)}; {len(missing)} of {len(entries)} file(s) to fetch, {gb(need)}; {gb(free)} free")
    if need > free:
        sys.exit("Not enough disk space for these models. Free some up, or download one group at a time.")

    opener = urllib.request.build_opener(DropAuthOnRedirect())
    token = os.environ.get("HF_TOKEN", "").strip()
    for entry in entries:
        download(entry, opener, token)
    print("all done")


if __name__ == "__main__":
    main()
