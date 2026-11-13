"""Draws generated worlds as a contact sheet, so a person can look at many at once.

    python world_sheet.py <folder of world JSON files> <out.png> [columns]

Each world is a WorldMap as the generator writes it (WorldMap.ToJson, rules v2 slice R2), exported
by GreyBoxStrategy.Editor.MapExportMenu or the generator tests. The sheet draws, from the
geometry's cell grid only, the way a screen would:

- the sea dark, inland water lighter, scenery islands in a plain land colour;
- each province in its terrain's colour, washed toward its starting owner's colour;
- province borders thin, realm borders and coasts heavier, ridge cuts as thick brown lines with
  the passes left open;
- capitals as white squares, ports as small blue dots, special places as gold diamonds;
- the province names in small type at each province's label point.

It needs Pillow. It reads only the JSON; nothing here decides anything about the world.
"""
import glob
import json
import os
import sys

from PIL import Image, ImageDraw, ImageFont

TERRAIN = {
    "plains": (196, 186, 120),
    "forest": (92, 128, 74),
    "hills": (164, 136, 92),
    "marsh": (112, 138, 124),
    "mountains": (126, 116, 110),
}
SEA = (30, 44, 60)
WATER = (64, 98, 122)
ISLAND = (170, 160, 120)
OWNERS = [
    (200, 60, 50), (50, 90, 190), (220, 170, 40), (60, 150, 80), (150, 70, 160),
    (40, 160, 170), (220, 110, 40), (120, 120, 120), (190, 80, 120), (100, 60, 30),
]


def decode(cells, n):
    out = []
    for run in cells.split(" "):
        v, k = run.split("*")
        out.extend([int(v)] * int(k))
    assert len(out) == n, (len(out), n)
    return out


def mix(a, b, t):
    return tuple(int(a[i] * (1 - t) + b[i] * t) for i in range(3))


def draw_world(world, scale, title_font, label_font):
    geo = world["geometry"]
    w, h = geo["width"], geo["height"]
    cells = decode(geo["cells"], w * h)
    provinces = world["provinces"]
    countries = [c["id"] for c in world["countries"]]
    colour = []
    for p in provinces:
        base = TERRAIN[p.get("terrain", "plains")]
        owner = p.get("owner")
        colour.append(mix(base, OWNERS[countries.index(owner) % len(OWNERS)], 0.55) if owner else base)

    img = Image.new("RGB", (w, h))
    px = img.load()
    for y in range(h):
        for x in range(w):
            v = cells[y * w + x]
            px[x, y] = SEA if v == -1 else WATER if v == -2 else ISLAND if v == -4 else colour[v]
    img = img.resize((w * scale, h * scale), Image.NEAREST)
    d = ImageDraw.Draw(img)
    u = max(1, scale // 3)  # line widths and markers grow with the drawing scale

    owner_of = [p.get("owner") for p in provinces]
    index = {p["id"]: i for i, p in enumerate(provinces)}
    for b in geo["borders"]:
        kind = b["kind"]
        if kind in ("coast", "shore"):
            fill, width = (20, 20, 20), 2 * u
        elif kind == "ridge":
            fill, width = (90, 50, 20), 5 * u
        else:
            a, c = index[b["a"]], index[b["b"]]
            same = owner_of[a] is not None and owner_of[a] == owner_of[c]
            realm_edge = owner_of[a] != owner_of[c] and (owner_of[a] or owner_of[c])
            fill = (60, 50, 40) if not realm_edge else (20, 15, 10)
            width = u if not realm_edge else 2 * u
            if same:
                fill = (110, 100, 80)
        for line in b["lines"]:
            pts = [(line[i] * scale, line[i + 1] * scale) for i in range(0, len(line), 2)]
            if len(pts) > 1:
                d.line(pts, fill=fill, width=width)
    for line in geo.get("islands", []):
        pts = [(line[i] * scale, line[i + 1] * scale) for i in range(0, len(line), 2)]
        if len(pts) > 1:
            d.line(pts, fill=(20, 20, 20), width=u)
    for r in geo.get("ridges", []):
        x0, y0, x1, y1 = [v * scale + scale // 2 for v in r["line"]]
        d.line([(x0, y0), (x1, y1)], fill=(90, 50, 20), width=u)

    specials = {s["province"]: s for s in world.get("specials", [])}
    for i, p in enumerate(provinces):
        shape = geo["provinces"][i]
        lx, ly = shape["label"][0] * scale + scale // 2, shape["label"][1] * scale + scale // 2
        cx, cy = shape["centre"][0] * scale + scale // 2, shape["centre"][1] * scale + scale // 2
        if p.get("capital"):
            d.rectangle([cx - 5 * u, cy - 5 * u, cx + 5 * u, cy + 5 * u], fill=(255, 255, 255), outline=(0, 0, 0), width=u)
        if p.get("port"):
            d.ellipse([cx + 6 * u, cy - 3 * u, cx + 12 * u, cy + 3 * u], fill=(120, 190, 255), outline=(0, 0, 0), width=u)
        if p["id"] in specials:
            d.polygon([(cx, cy - 9 * u), (cx + 7 * u, cy), (cx, cy + 9 * u), (cx - 7 * u, cy)], fill=(240, 200, 60), outline=(0, 0, 0))
        name = p["name"]
        tw = d.textlength(name, font=label_font)
        d.text((lx - tw / 2, ly + 7 * u), name, fill=(15, 15, 15), font=label_font)

    return img


def title_of(world):
    gen = world.get("generated") or {}
    st = gen.get("settings") or {}
    return (f"{world.get('name', '')}  seed {gen.get('seed')}  {st.get('size', '')} {st.get('shape', '')}  "
            f"{len(world['provinces'])} provinces, {len(world['countries'])} countries, "
            f"{gen.get('neutralPercent', '?')}% neutral, {len(world.get('specials', []))} special")


def main(folder, out, columns=4):
    files = sorted(glob.glob(os.path.join(folder, "*.json")))
    if not files:
        sys.exit(f"no world JSON in {folder}")
    try:
        title_font = ImageFont.truetype("arial.ttf", 15)
        label_font = ImageFont.truetype("arial.ttf", 10)
    except OSError:
        title_font = label_font = ImageFont.load_default()
    tw, th = 900, 520
    tiles = []
    for f in files:
        world = json.load(open(f, encoding="utf-8"))
        geo = world["geometry"]
        # Draw at about twice the tile's size, then scale down, so every world fills its tile alike.
        scale = max(1, -(-1800 // geo["width"]))
        tile = draw_world(world, scale, title_font, ImageFont.truetype("arial.ttf", 3 * scale) if label_font != title_font else label_font)
        k = min((tw - 10) / tile.width, (th - 10) / tile.height)
        tiles.append((title_of(world), tile.resize((int(tile.width * k), int(tile.height * k)), Image.LANCZOS)))
    rows = (len(tiles) + columns - 1) // columns
    bar = 24
    sheet = Image.new("RGB", (columns * tw, rows * (th + bar)), (12, 16, 22))
    d = ImageDraw.Draw(sheet)
    for k, (title, tile) in enumerate(tiles):
        x, y = (k % columns) * tw, (k // columns) * (th + bar)
        d.text((x + 6, y + 4), title, fill=(235, 235, 235), font=title_font)
        sheet.paste(tile, (x + (tw - tile.width) // 2, y + bar))
    sheet.save(out)
    print(f"{len(tiles)} worlds -> {out}")


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2], int(sys.argv[3]) if len(sys.argv) > 3 else 4)
