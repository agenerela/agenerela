"""Builds Painted Realm's art pack from chosen candidates (#20).

    python build_pack.py <candidates folder>

The candidates folder is what the asset server made (`tools/comfyui/comfy.py run image`), laid out as
`rulers/<face>-<candidate>.png`, `generals/<face>-<candidate>.png` and `events/<id>-<candidate>.png`.
`pack.json` beside this script says which candidate of each was chosen, with its prompt and seed.

It writes, under Assets/GreyBoxStrategy/Screen/Art/Resources/ArtPack/:
  portraits/<face>.jpg        480 x 560, for frames above 80 px
  portraits-small/<face>.jpg  192 x 224, for the panels' frames (52 x 62, 58 x 66) at up to three times the size
  events/<id>.jpg             1024 x 576
  README.md                   every file's prompt and seed
and prints the path of a contact sheet of the whole pack. Needs Pillow.
"""
import json
import sys
from pathlib import Path

from PIL import Image, ImageDraw

HERE = Path(__file__).resolve().parent
PACK = HERE.parent.parent / "Assets/GreyBoxStrategy/Screen/Art/Resources/ArtPack"
LARGE, SMALL, EVENT = (480, 560), (192, 224), (1024, 576)


# A thin painted edge line sometimes runs round a picture; 2.5% off every side removes it and keeps the shape.
INSET = 0.025


def save(image, size, path):
    path.parent.mkdir(parents=True, exist_ok=True)
    w, h = image.size
    dx, dy = round(w * INSET), round(h * INSET)
    image = image.convert("RGB").crop((dx, dy, w - dx, h - dy))
    image.resize(size, Image.LANCZOS).save(path, quality=90, optimize=True)


def main():
    candidates = Path(sys.argv[1])
    pack = json.loads((HERE / "pack.json").read_text(encoding="utf-8"))
    lines = [
        "# Painted Realm: the art pack",
        "",
        "Pictures made ahead of time on the team's asset server (`tools/comfyui/comfy.py run image`, Z-Image Turbo,",
        "8 steps) and chosen by looking at two or more candidates each, then trimmed by 2.5% a side and resized. `ArtCatalogue.cs` says which picture a ruler,",
        "a general or an event shows. No picture shows a real person, a named artist's style or any lettering.",
        "",
        "To remake one: `python tools/comfyui/comfy.py run image --set \"prompt=<its prompt>\" --set seed=<its seed>",
        "--set width=<w> --set height=<h>`, then `python Demos/GreyBoxStrategy/ArtSource/art-pack/build_pack.py",
        "<folder>` resizes the chosen candidates into this folder. The choices are in `ArtSource/art-pack/pack.json`.",
        "",
        "Every prompt is the style line, then the kind's line, then the subject.",
        "",
        f"- **Style:** {pack['style']}",
        f"- **A portrait** (768 x 896): {pack['portrait']}",
        f"- **An event** (1024 x 576): {pack['scene']}",
        "",
    ]
    sheet_cells = []
    for kind, title, folder in (("rulers", "Rulers", "rulers"), ("generals", "Generals", "generals"), ("events", "Events", "events")):
        lines += [f"## {title}", "", "| File | Seed | Subject |", "|---|---|---|"]
        for name, entry in pack[kind].items():
            source = candidates / folder / f"{name}-{entry['candidate']}.png"
            image = Image.open(source)
            if kind == "events":
                save(image, EVENT, PACK / "events" / f"{name}.jpg")
                shown = f"events/{name}.jpg"
            else:
                save(image, LARGE, PACK / "portraits" / f"{name}.jpg")
                save(image, SMALL, PACK / "portraits-small" / f"{name}.jpg")
                shown = f"portraits/{name}.jpg"
            lines.append(f"| `{shown}` | {entry['seed']} | {entry['subject']} |")
            sheet_cells.append((kind, name, image.convert("RGB")))
        lines.append("")
    (PACK / "README.md").write_text("\n".join(lines), encoding="utf-8", newline="\n")

    # The contact sheet: portraits eight across, events four across.
    portraits = [c for c in sheet_cells if c[0] != "events"]
    events = [c for c in sheet_cells if c[0] == "events"]
    pw, ph, ew, eh, label = 200, 233, 400, 225, 16
    prow, erow = -(-len(portraits) // 8), -(-len(events) // 4)
    sheet = Image.new("RGB", (1600, prow * (ph + label) + erow * (eh + label)), (24, 20, 16))
    draw = ImageDraw.Draw(sheet)
    for i, (_, name, image) in enumerate(portraits):
        x, y = (i % 8) * pw, (i // 8) * (ph + label)
        sheet.paste(image.resize((pw, ph), Image.LANCZOS), (x, y))
        draw.text((x + 4, y + ph + 2), name, fill=(230, 210, 160))
    top = prow * (ph + label)
    for i, (_, name, image) in enumerate(events):
        x, y = (i % 4) * ew, top + (i // 4) * (eh + label)
        sheet.paste(image.resize((ew, eh), Image.LANCZOS), (x, y))
        draw.text((x + 4, y + eh + 2), name, fill=(230, 210, 160))
    out = candidates / "contact-sheet.jpg"
    sheet.save(out, quality=88)
    print(out)


if __name__ == "__main__":
    main()
