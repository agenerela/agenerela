"""A run request, docs/run-requests/NNN-<name>/run.json: reading it, checking it, building a block's command.

DESIGN.md, Part 2, describes the fields. Two the design leaves implicit are spelled out here:

- block_args: the per-block part of the command, a template. The default suits GreyBoxStrategy's
  RunInterleavedFromCommandLine; another game's request gives its own.
- command: instead of project and method, a whole command template (a list of strings). It runs a
  block without Unity: the kit's tests use it, and so can a scripted check.

Placeholders, in block_args, extra, command and done_when: {name} (also {block}), {arm}, {seed},
{index}, {games}, {model}, {out} (the results folder), {checkout} (the worktree), {python}.
"""
import re
import shlex
from pathlib import Path

from .common import RunnerError, read_json

DEFAULT_BLOCK_ARGS = ("-strategyModel {model} -strategyArms {arm} -strategyArmNames {name} "
                      "-strategyGames {games} -strategySeed {seed} -strategyBlock {games} "
                      "-strategyIndex {index} -strategyOut {out}")

# What a game's log says when the model could not be asked, so the block measured nothing.
DEFAULT_INVALID_IF_LOG = ["PipelineErrorKind.ProviderFailed", "The provider returned no result"]

NAME = re.compile(r"^[A-Za-z0-9][A-Za-z0-9_.-]*$")
SHA = re.compile(r"^[0-9a-f]{40}$")
FLAG = re.compile(r"^-{1,2}[A-Za-z]")
KNOWN = {"id", "question", "commit", "project", "method", "command", "model", "common_args",
         "block_args", "blocks", "done_when", "smallest_slice", "report", "keep", "notes",
         "invalid_if_log"}
BLOCK_KNOWN = {"name", "arm", "seed", "index", "games", "extra", "minutes"}


class Block:
    def __init__(self, data):
        self.data = data
        self.name = data.get("name")
        self.arm = data.get("arm", "default")
        self.seed = data.get("seed", 1)
        self.index = data.get("index", 0)
        self.games = data.get("games", 1)
        self.extra = data.get("extra", "")
        self.minutes = data.get("minutes")

    def values(self, request, out, checkout, python):
        return {"name": self.name, "block": self.name, "arm": self.arm, "seed": self.seed,
                "index": self.index, "games": self.games, "model": request.model,
                "out": str(out), "checkout": str(checkout), "python": python}


class Request:
    def __init__(self, data, folder=None):
        self.data = data
        self.folder = Path(folder) if folder else None
        self.id = data.get("id")
        self.question = data.get("question", "")
        self.commit = data.get("commit", "")
        self.project = data.get("project")
        self.method = data.get("method")
        self.command = data.get("command")
        self.model = data.get("model", "")
        self.common_args = data.get("common_args", "")
        self.block_args = data.get("block_args", DEFAULT_BLOCK_ARGS)
        self.blocks = [Block(b) for b in data.get("blocks") or [] if isinstance(b, dict)]
        self.done_when = data.get("done_when", "")
        self.smallest_slice = data.get("smallest_slice") or []
        self.report = data.get("report") or []
        self.keep = data.get("keep") or []
        self.invalid_if_log = data.get("invalid_if_log", DEFAULT_INVALID_IF_LOG)

    @classmethod
    def load(cls, folder):
        folder = Path(folder)
        path = folder / "run.json" if folder.is_dir() or not folder.suffix else folder
        if not path.is_file():
            raise RunnerError(f"no run.json at {path}")
        try:
            data = read_json(path)
        except ValueError as e:
            raise RunnerError(f"{path} is not valid JSON: {e}")
        return cls(data, path.parent)

    def block(self, name):
        for b in self.blocks:
            if b.name == name:
                return b
        return None

    def play_order(self):
        """The smallest slice first, in its own order, then every other block as listed."""
        first = [self.block(n) for n in self.smallest_slice if self.block(n)]
        return first + [b for b in self.blocks if b.name not in self.smallest_slice]

    def total_minutes(self):
        return sum(b.minutes or 0 for b in self.blocks)

    def command_for(self, block, out, checkout, python, unity_exe=None, log_file=None):
        """The block's command as a list, without any key in it (the key goes in the environment)."""
        values = block.values(self, out, checkout, python)
        if self.command:
            args = [fill(part, values) for part in self.command]
            return args + filled(block.extra, values)
        project = Path(checkout) / self.project
        args = [unity_exe or "Unity.exe", "-batchmode", "-nographics", "-projectPath", str(project),
                "-executeMethod", self.method]
        args += filled(self.common_args, values)
        args += filled(self.block_args, values)
        args += filled(block.extra, values)
        if log_file:
            args += ["-logFile", str(log_file)]
        return args

    def done_path(self, block, out):
        return Path(out) / fill(self.done_when, block.values(self, out, "", ""))

    def problems(self):
        """Everything wrong with the request that can be seen without git; empty when it is fine."""
        p = []
        d = self.data
        if not isinstance(d, dict):
            return ["run.json is not an object"]
        for key in sorted(set(d) - KNOWN):
            p.append(f"unknown field {key!r}")
        if not self.id or not NAME.match(str(self.id)):
            p.append("id is missing or has characters other than letters, digits, '.', '_' and '-'")
        elif self.folder is not None and self.folder.name != self.id:
            p.append(f"id {self.id!r} does not match its folder {self.folder.name!r}")
        if not self.question:
            p.append("question is missing: say what the run answers")
        if not SHA.match(str(self.commit)):
            p.append("commit must be a full 40-character sha")
        if self.command is not None:
            if not isinstance(self.command, list) or not self.command or not all(isinstance(c, str) for c in self.command):
                p.append("command must be a non-empty list of strings")
            if self.project or self.method:
                p.append("give command, or project and method, not both")
        else:
            if not self.project:
                p.append("project is missing, such as Demos/GreyBoxStrategy")
            if not self.method:
                p.append("method is missing: the -executeMethod to call")
        for key in ("common_args", "block_args"):
            if not isinstance(d.get(key, ""), str):
                p.append(f"{key} must be a string")
        if not self.done_when:
            p.append("done_when is missing, such as {block}/summary.md")
        if not self.blocks:
            p.append("blocks is missing or empty")
        names = set()
        for i, b in enumerate(self.blocks):
            where = f"block {i + 1} ({b.name or 'no name'})"
            for key in sorted(set(b.data) - BLOCK_KNOWN):
                p.append(f"{where}: unknown field {key!r}")
            if not b.name or not NAME.match(str(b.name)):
                p.append(f"{where}: name is missing or has characters other than letters, digits, '.', '_' and '-'")
            elif b.name in names:
                p.append(f"{where}: the name is used twice")
            names.add(b.name)
            for key in ("seed", "index", "games"):
                if not isinstance(getattr(b, key), int) or isinstance(getattr(b, key), bool):
                    p.append(f"{where}: {key} must be a whole number")
            if isinstance(b.games, int) and b.games < 1:
                p.append(f"{where}: games must be at least 1")
            if not isinstance(b.minutes, (int, float)) or isinstance(b.minutes, bool) or b.minutes <= 0:
                p.append(f"{where}: minutes, its estimate, must be a number above 0")
            if not isinstance(b.extra, str):
                p.append(f"{where}: extra must be a string")
        if len(self.blocks) != len(d.get("blocks") or []):
            p.append("every block must be an object")
        for n in self.smallest_slice:
            if n not in names:
                p.append(f"smallest_slice names {n!r}, which is not a block")
        if not all(isinstance(x, str) for x in self.report):
            p.append("report must be a list of strings")
        if not all(isinstance(x, str) for x in self.keep):
            p.append("keep must be a list of glob strings")
        if not p:
            p += self.flag_problems()
        return p

    def flag_problems(self):
        """A flag given twice: the game reads the first copy, so a block's own setting would be lost."""
        p = []
        for b in self.blocks:
            try:
                args = self.command_for(b, "OUT", "CHECKOUT", "python", log_file="LOG")
            except (ValueError, KeyError) as e:
                p.append(f"block {b.name}: cannot build its command: {e}")
                continue
            seen = {}
            for a in args:
                if FLAG.match(a):
                    seen[a] = seen.get(a, 0) + 1
            twice = sorted(f for f, n in seen.items() if n > 1)
            if twice:
                p.append(f"block {b.name}: {', '.join(twice)} given more than once (the game reads only "
                         "the first, so put the block's whole value in extra and leave it out of common_args)")
        return p


def fill(text, values):
    try:
        return str(text).format(**values)
    except KeyError as e:
        raise KeyError(f"unknown placeholder {{{e.args[0]}}} in {text!r}")


def filled(template, values):
    """Split a template the way a shell would, then fill each word, so a path with a space stays one word."""
    return [fill(word, values) for word in split(template)]


def split(text):
    return shlex.split(text, posix=True) if text else []
