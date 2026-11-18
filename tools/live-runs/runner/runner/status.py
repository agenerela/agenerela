"""status.json: what the runner is doing, rewritten after every block and every minute while it waits.

It holds the request and block in hand, the blocks done of the total, the minutes estimated to be
left, the last error, whether the card is the runner's, and the time. Never a key or an address.
"""
from pathlib import Path

from .common import now, write_json


class Status:
    def __init__(self, path, runner):
        self.path = Path(path) if path else None
        self.data = {"runner": runner, "state": "starting", "request": None, "block": None,
                     "blocks_done": 0, "blocks_total": 0, "minutes_left": 0, "card_in_use": False,
                     "waiting": "", "last_error": "", "time": now()}

    def write(self, **fields):
        self.data.update(fields)
        self.data["time"] = now()
        if self.path:
            write_json(self.path, self.data)

    def progress(self, request, manifest, card=None, **fields):
        counts = manifest.counts()
        self.write(request=request.id, blocks_done=counts["done"], blocks_total=len(request.blocks),
                   minutes_left=round(manifest.minutes_left(request), 1),
                   card_in_use=bool(card and card.in_use), waiting=getattr(card, "waiting", "") if card else "",
                   **fields)
