"""The run queue's tests: python -m unittest discover -s tools/live-runs/runner/tests

No Unity, no GPU, no network: blocks are tests/fake_block.py, the card is a NullCard, and the queue
is read from a dictionary instead of origin/test.
"""
import json
import subprocess
import sys
import tempfile
import time
import unittest
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent))

from runner import gitio, queuefile  # noqa: E402
from runner.card import NullCard  # noqa: E402
from runner.manifest import Manifest  # noqa: E402
from runner.play import Player  # noqa: E402
from runner.request import Request  # noqa: E402
from runner.serve import Server  # noqa: E402
from runner.status import Status  # noqa: E402

SHA = "0123456789abcdef0123456789abcdef01234567"
FAKE = str(HERE / "fake_block.py")


def fake_request(request_id, blocks, **more):
    data = {"id": request_id, "question": "Does the runner resume?", "commit": SHA,
            "command": ["{python}", FAKE, "{out}", "{name}"], "done_when": "{block}/summary.md",
            "keep": ["**/summary.md"],
            "blocks": [dict({"name": n, "arm": "default", "seed": i + 1, "index": 0, "games": 1, "minutes": 0.05},
                            **({"extra": e} if isinstance(e, str) else e))
                       for i, (n, e) in enumerate(blocks)]}
    data.update(more)
    return data


def write_request(root, data):
    folder = Path(root) / data["id"]
    folder.mkdir(parents=True, exist_ok=True)
    (folder / "run.json").write_text(json.dumps(data), encoding="utf-8")
    return folder


def plays(out):
    p = Path(out) / "plays.txt"
    return p.read_text(encoding="utf-8").split() if p.exists() else []


class Temp(unittest.TestCase):
    def setUp(self):
        self._dir = tempfile.TemporaryDirectory()
        self.root = Path(self._dir.name)
        self.checkout = self.root / "checkout"
        self.checkout.mkdir()

    def tearDown(self):
        self._dir.cleanup()

    def player(self, data, name="out", card=None):
        request = Request(data, write_request(self.root / "requests", data))
        return Player(request, self.root / name, self.checkout, card or NullCard(), "test", poll=0.02)


class RequestTests(unittest.TestCase):
    def good(self):
        return {"id": "007-ab", "question": "Which arm?", "commit": SHA, "project": "Demos/GreyBoxStrategy",
                "method": "GreyBoxStrategy.Editor.StrategyTableMenu.RunInterleavedFromCommandLine",
                "model": "qwen3.5:4b", "common_args": "-strategyTable personas -strategyTurn writs",
                "blocks": [{"name": "c1", "arm": "default", "seed": 1, "index": 0, "games": 1,
                            "extra": "-strategyRules FreeOrders=true", "minutes": 6},
                           {"name": "f1", "arm": "default", "seed": 1, "index": 0, "games": 1,
                            "extra": "-strategyRules FreeOrders=true;CourtTurnFacts=true", "minutes": 6}],
                "done_when": "{block}/summary.md", "smallest_slice": ["c1", "f1"]}

    def test_a_good_request_has_no_problems(self):
        self.assertEqual(Request(self.good(), Path("007-ab")).problems(), [])

    def test_a_flag_in_both_common_args_and_extra_is_refused(self):
        # The design's first example did this: the game reads the first -strategyRules only, so every
        # arm would have played FreeOrders alone.
        data = self.good()
        data["common_args"] += " -strategyRules FreeOrders=true"
        data["blocks"][1]["extra"] = "-strategyRules CourtTurnFacts=true"
        problems = Request(data, Path("007-ab")).problems()
        self.assertTrue(any("-strategyRules given more than once" in p for p in problems), problems)

    def test_a_flag_the_runner_sets_is_refused_in_extra(self):
        data = self.good()
        data["blocks"][0]["extra"] = "-strategyModel qwen3.5:2b"
        self.assertTrue(any("-strategyModel" in p for p in Request(data, Path("007-ab")).problems()))

    def test_missing_and_wrong_fields_are_named(self):
        data = self.good()
        data["commit"] = "abc"
        data["blocks"][0]["minutes"] = 0
        data["blocks"][1]["name"] = "c1"
        data["smallest_slice"] = ["zz"]
        data["colour"] = "blue"
        problems = " | ".join(Request(data, Path("007-ab")).problems())
        for words in ("full 40-character sha", "minutes", "used twice", "'zz'", "unknown field 'colour'"):
            self.assertIn(words, problems)

    def test_the_id_must_match_its_folder(self):
        self.assertTrue(any("does not match its folder" in p for p in Request(self.good(), Path("008-x")).problems()))

    def test_the_unity_command_is_built_in_order_with_paths_kept_whole(self):
        r = Request(self.good(), Path("007-ab"))
        args = r.command_for(r.blocks[1], "C:/out dir", "C:/wt", "python", unity_exe="Unity.exe", log_file="C:/out dir/f1.log")
        self.assertEqual(args[:7], ["Unity.exe", "-batchmode", "-nographics", "-projectPath",
                                    str(Path("C:/wt") / "Demos/GreyBoxStrategy"), "-executeMethod",
                                    "GreyBoxStrategy.Editor.StrategyTableMenu.RunInterleavedFromCommandLine"])
        self.assertEqual(args[args.index("-strategyOut") + 1], "C:/out dir")
        self.assertEqual(args[args.index("-strategyArmNames") + 1], "f1")
        self.assertEqual(args[args.index("-strategyRules") + 1], "FreeOrders=true;CourtTurnFacts=true")
        self.assertEqual(args[-2:], ["-logFile", "C:/out dir/f1.log"])

    def test_the_smallest_slice_plays_first(self):
        data = self.good()
        data["smallest_slice"] = ["f1"]
        self.assertEqual([b.name for b in Request(data, Path("007-ab")).play_order()], ["f1", "c1"])


class ResumeTests(Temp):
    def test_every_block_plays_once_and_is_done(self):
        p = self.player(fake_request("001-r", [("a", "0"), ("b", "0"), ("c", "0")]))
        self.assertEqual(p.play(), ("finished", None))
        self.assertEqual(plays(p.out), ["a", "start", "a", "end", "b", "start", "b", "end", "c", "start", "c", "end"])
        self.assertEqual([b["status"] for b in p.manifest.data["blocks"]], ["done"] * 3)
        # a second run plays nothing
        again = self.player(fake_request("001-r", [("a", "0"), ("b", "0"), ("c", "0")]))
        self.assertEqual(again.play(), ("finished", None))
        self.assertEqual(len(plays(p.out)), 12)

    def test_a_run_stopped_mid_block_resumes_without_replaying_finished_blocks(self):
        data = fake_request("002-r", [("a", "0"), ("b", "30"), ("c", "0")])
        for b in data["blocks"]:
            b["minutes"] = 2
        folder = write_request(self.root / "requests", data)
        out = self.root / "out"
        driver = subprocess.Popen([sys.executable, str(HERE / "play_fake.py"), str(folder), str(out), str(self.checkout)])
        try:
            deadline = time.time() + 30
            while plays(out)[:6] != ["a", "start", "a", "end", "b", "start"]:
                self.assertLess(time.time(), deadline, f"block b never started: {plays(out)}")
                time.sleep(0.05)
            while not json.loads((out / "manifest.json").read_text(encoding="utf-8"))["blocks"][1].get("exe"):
                self.assertLess(time.time(), deadline, "block b's process was never recorded")
                time.sleep(0.05)
            entry = json.loads((out / "manifest.json").read_text(encoding="utf-8"))["blocks"][1]
            self.assertEqual(entry["status"], "running")
            # Kill the runner hard, as a crash or a reboot would: block b's process is left behind.
            driver.kill()
            driver.wait()
        finally:
            if driver.poll() is None:
                driver.kill()
        self.assertTrue((out / "b" / "partial.txt").exists())

        data["blocks"][1]["extra"] = "0"  # the same block, quick this time
        p = self.player(data)
        self.assertEqual(p.play(), ("finished", None))
        self.assertEqual(plays(out)[6:], ["b", "start", "b", "end", "c", "start", "c", "end"])
        self.assertEqual(plays(out).count("a"), 2, "block a played again")
        b = p.manifest.entry("b")
        self.assertEqual((b["status"], b["tries"]), ("done", 1))
        self.assertIn("found running", b["interrupted"][0])
        aside = list((out / "_aside").glob("b-*"))
        self.assertEqual(len(aside), 1, "the partial output was not set aside")
        self.assertTrue((aside[0] / "b" / "partial.txt").exists())

    def test_a_request_at_another_commit_is_refused(self):
        data = fake_request("003-r", [("a", "0")])
        self.player(data).play()
        data["commit"] = "f" * 40
        with self.assertRaises(Exception) as caught:
            self.player(data)
        self.assertIn("never edited", str(caught.exception))

    def test_a_deleted_entry_plays_again(self):
        data = fake_request("004-r", [("a", "0"), ("b", "0")])
        p = self.player(data)
        p.play()
        path = p.out / "manifest.json"
        m = json.loads(path.read_text(encoding="utf-8"))
        m["blocks"] = [b for b in m["blocks"] if b["name"] != "a"]
        path.write_text(json.dumps(m), encoding="utf-8")
        self.player(data).play()
        self.assertEqual(plays(p.out)[-2:], ["a", "end"])


class TimeoutTests(Temp):
    def test_a_hung_block_is_killed_at_twice_its_estimate_retried_once_then_left(self):
        # estimate 0.01 min = 0.6 s, so the kill comes at 1.2 s; the block would sleep 30 s
        data = fake_request("005-t", [("hang", {"extra": "30", "minutes": 0.01}), ("next", "0")])
        p = self.player(data)
        started = time.monotonic()
        self.assertEqual(p.play(), ("finished", None))
        took = time.monotonic() - started
        self.assertLess(took, 15, "the hung block was not killed")
        hang = p.manifest.entry("hang")
        self.assertEqual((hang["status"], hang["tries"]), ("invalid: timeout", 2))
        self.assertEqual(plays(p.out).count("hang"), 2)
        self.assertEqual(p.manifest.status("next"), "done")
        self.assertGreaterEqual(hang["minutes"], 0.02)

    def test_exit_codes_provider_failures_and_missing_output_are_invalid(self):
        data = fake_request("006-t", [("crash", "0 exit 3"), ("provider", "0 say PipelineErrorKind.ProviderFailed exit 0"),
                                      ("nothing", "0 exit 0"), ("ok", "0")])
        p = self.player(data)
        p.play()
        self.assertEqual(p.manifest.status("crash"), "invalid: exit 3")
        self.assertEqual(p.manifest.status("provider"), "invalid: provider")
        self.assertEqual(p.manifest.status("nothing"), "invalid: no nothing/summary.md")
        self.assertEqual(p.manifest.status("ok"), "done")

    def test_a_provider_failure_in_the_log_is_invalid_even_with_output(self):
        data = fake_request("007-t", [("p", "0 say PipelineErrorKind.ProviderFailed")])
        p = self.player(data)
        p.play()
        self.assertEqual(p.manifest.status("p"), "invalid: provider")


class PauseTests(Temp):
    def test_a_pause_takes_effect_after_the_block_in_hand_and_resume_plays_the_rest_once(self):
        data = fake_request("008-p", [("a", "0"), ("b", "0"), ("c", "0")])
        pause_file = self.root / "pause"
        p = self.player(data, card=NullCard(pause_file))
        # the pause file appears while block a plays, as a person at the machine would make it
        p.after = None
        played = p.play_block

        def play_block(block):
            pause_file.write_text("", encoding="utf-8")
            return played(block)
        p.play_block = play_block
        self.assertEqual(p.play(), ("stopped", f"paused by {pause_file}"))
        pause_file.unlink()
        self.assertEqual(plays(p.out), ["a", "start", "a", "end"])
        resumed = self.player(data)
        self.assertEqual(resumed.play(), ("finished", None))
        self.assertEqual(plays(p.out), ["a", "start", "a", "end", "b", "start", "b", "end", "c", "start", "c", "end"])


class FakeServer(Server):
    """The serve loop with the queue and requests from memory instead of origin/test."""

    def __init__(self, root, checkout, queue, requests):
        super().__init__(root, Path(root) / "work", NullCard(), "test", Status(None, "test"), push=False, idle=0, once=True)
        self.queue, self.requests, self.checkout = queue, requests, checkout
        self.after_hooks = []

    def read(self):
        for hook in list(self.after_hooks):
            hook()
        return json.loads(json.dumps(self.queue)), {i: Request(d, Path(i)) for i, d in self.requests.items()}


class QueueTests(Temp):
    def setUp(self):
        super().setUp()
        self._worktree = gitio.worktree
        gitio.worktree = lambda repo, work, sha: self.checkout

    def tearDown(self):
        gitio.worktree = self._worktree
        super().tearDown()

    def test_the_queue_file_is_checked(self):
        self.assertEqual(queuefile.check({"paused": False, "order": ["a"], "hold": ["b"]}), [])
        self.assertTrue(queuefile.check({"order": ["a"], "hold": ["a"]}))
        self.assertTrue(queuefile.check({"order": ["a", "a"]}))
        self.assertTrue(queuefile.check({"paused": "no"}))
        self.assertEqual(queuefile.parse(None)["order"], [])

    def test_front_moves_a_request_and_takes_it_off_hold(self):
        q = {"paused": False, "pause_reason": "", "order": ["a", "b"], "hold": ["c"]}
        queuefile.front(q, "c")
        self.assertEqual((q["order"], q["hold"]), (["c", "a", "b"], []))
        queuefile.front(q, "b")
        self.assertEqual(q["order"], ["b", "c", "a"])
        queuefile.pause(q, "a game")
        self.assertEqual((q["paused"], q["pause_reason"]), (True, "a game"))
        queuefile.resume(q)
        self.assertEqual((q["paused"], q["pause_reason"]), (False, ""))

    def test_requests_play_in_order_and_held_ones_wait(self):
        a = fake_request("a", [("a1", "0")])
        b = fake_request("b", [("b1", "0")])
        s = FakeServer(self.root, self.checkout, {"paused": False, "pause_reason": "", "order": ["a", "b"], "hold": ["b"]},
                       {"a": a, "b": b})
        s.serve()
        self.assertEqual(plays(self.root / "work" / "results" / "a"), ["a1", "start", "a1", "end"])
        self.assertEqual(plays(self.root / "work" / "results" / "b"), [])
        self.assertEqual(s.status.data["state"], "idle")

    def test_a_request_moved_to_the_front_runs_next_and_the_one_it_interrupted_finishes_later(self):
        a = fake_request("a", [("a1", "0"), ("a2", "0"), ("a3", "0")])
        b = fake_request("b", [("b1", "0"), ("b2", "0")])
        s = FakeServer(self.root, self.checkout, {"paused": False, "pause_reason": "", "order": ["a", "b"], "hold": []},
                       {"a": a, "b": b})
        log = self.root / "order.txt"

        def switch_after_a1():
            done = plays(self.root / "work" / "results" / "a")
            if done == ["a1", "start", "a1", "end"] and s.queue["order"][0] == "a":
                queuefile.front(s.queue, "b")
        s.after_hooks.append(switch_after_a1)
        s.serve()
        a_out, b_out = self.root / "work" / "results" / "a", self.root / "work" / "results" / "b"
        self.assertEqual([w for w in plays(a_out) if w not in ("start", "end")], ["a1", "a1", "a2", "a2", "a3", "a3"])
        self.assertEqual(plays(b_out).count("b1"), 2)
        # b played between a1 and a2: b's manifest finished before a2 started
        ma = json.loads((a_out / "manifest.json").read_text(encoding="utf-8"))
        mb = json.loads((b_out / "manifest.json").read_text(encoding="utf-8"))
        a1_end = ma["blocks"][0]["ended"]
        a2_start = ma["blocks"][1]["started"]
        b_end = mb["blocks"][1]["ended"]
        self.assertTrue(a1_end <= mb["blocks"][0]["started"] and b_end <= a2_start, (a1_end, b_end, a2_start))
        self.assertFalse(log.exists())

    def test_paused_in_the_queue_stops_after_the_block_and_resume_continues(self):
        a = fake_request("a", [("a1", "0"), ("a2", "0")])
        s = FakeServer(self.root, self.checkout, {"paused": False, "pause_reason": "", "order": ["a"], "hold": []}, {"a": a})

        def pause_after_a1():
            if plays(self.root / "work" / "results" / "a") == ["a1", "start", "a1", "end"]:
                queuefile.pause(s.queue, "playing a game")
        s.after_hooks.append(pause_after_a1)
        s.serve()
        out = self.root / "work" / "results" / "a"
        self.assertEqual(plays(out), ["a1", "start", "a1", "end"])
        self.assertEqual(s.status.data["state"], "paused")
        self.assertIn("playing a game", s.status.data["waiting"])
        s.after_hooks.clear()
        queuefile.resume(s.queue)
        s.serve()
        self.assertEqual(plays(out), ["a1", "start", "a1", "end", "a2", "start", "a2", "end"])

    def test_a_local_pause_file_pauses_the_runner(self):
        a = fake_request("a", [("a1", "0")])
        s = FakeServer(self.root, self.checkout, {"paused": False, "pause_reason": "", "order": ["a"], "hold": []}, {"a": a})
        s.work.mkdir(parents=True, exist_ok=True)
        (s.work / "pause").write_text("", encoding="utf-8")
        s.serve()
        self.assertEqual(plays(self.root / "work" / "results" / "a"), [])
        self.assertIn("paused on this machine", s.status.data["waiting"])


class ResultsTests(Temp):
    def test_keep_and_the_size_limits_decide_what_goes_on_the_branch(self):
        out = self.root / "out"
        (out / "c1").mkdir(parents=True)
        (out / "_aside" / "c1-x").mkdir(parents=True)
        (out / "manifest.json").write_text("{}", encoding="utf-8")
        (out / "c1" / "summary.md").write_text("s", encoding="utf-8")
        (out / "c1" / "big.recording.json").write_bytes(b"x" * 2000)
        (out / "c1" / "raw.log").write_text("log", encoding="utf-8")
        (out / "_aside" / "c1-x" / "summary.md").write_text("old", encoding="utf-8")
        dest = self.root / "dest"
        limit = gitio.MAX_FILE_BYTES
        gitio.MAX_FILE_BYTES = 1000
        try:
            left = gitio.copy_results(out, dest, ["**/summary.md", "**/*.recording.json"])
        finally:
            gitio.MAX_FILE_BYTES = limit
        self.assertEqual(left, 1)
        self.assertTrue((dest / "manifest.json").exists())
        self.assertTrue((dest / "c1" / "summary.md").exists())
        self.assertFalse((dest / "c1" / "raw.log").exists())
        self.assertFalse((dest / "_aside").exists())
        self.assertIn("c1/big.recording.json", (dest / "LEFT_ON_MACHINE.md").read_text(encoding="utf-8"))

    def test_only_runs_branches_are_pushed(self):
        self.assertTrue(gitio.RESULTS_PREFIX == "refs/heads/runs/")
        r = Request(fake_request("../test", [("a", "0")]), None)
        with self.assertRaises(Exception):
            gitio.publish(self.root, self.root / "work", r, self.root / "out")


if __name__ == "__main__":
    unittest.main()
