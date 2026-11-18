"""Play a request with no card, for the resume test: python play_fake.py <run.json> <out> <checkout>."""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
from runner.card import NullCard  # noqa: E402
from runner.play import Player  # noqa: E402
from runner.request import Request  # noqa: E402

request = Request.load(sys.argv[1])
Player(request, sys.argv[2], sys.argv[3], NullCard(), "test", poll=0.05).play()
