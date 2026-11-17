"""Living Vale's shared palette: one 64x64 PNG of 8x8 colour cells, used by every model.

Every face of every model has all its UVs on the centre of one cell, so it reads as one flat
colour, and the whole kit shares one texture and one material. The cell order below is the
file format: append new colours at the end, never reorder, or every exported UV moves.

Colours are grounded medieval: weathered stone, aged timber, undyed and plant-dyed cloth,
with Vale blue as the one saturated accent the player and Mara's canopy share.
"""

CELLS = 8          # cells per side
CELL_PX = 8        # pixels per cell
SIZE = CELLS * CELL_PX

# name -> sRGB hex. Index = position in this list.
COLOURS = [
    # stone and earth
    ("stone_light", "#a59f92"), ("stone", "#857f74"), ("stone_dark", "#5e5a53"), ("mortar", "#b9b1a0"),
    ("slate", "#4f565c"), ("earth", "#6b5440"), ("ash", "#3b3835"), ("moss", "#5f6b3a"),
    # timber and thatch
    ("timber_light", "#9c7a52"), ("timber", "#76563a"), ("timber_dark", "#4d3826"), ("plaster", "#d8ccb4"),
    ("thatch", "#b39a5c"), ("thatch_dark", "#8a7444"), ("rope", "#a68f68"), ("burlap", "#9a8460"),
    # metal and leather
    ("iron", "#3e4144"), ("steel", "#9aa2a8"), ("brass", "#a8873e"), ("leather", "#6e4a2e"),
    ("leather_dark", "#3f2a1c"), ("canvas", "#c9bc9c"), ("soot", "#262422"), ("glass", "#46575c"),
    # cloth
    ("vale_blue", "#33628c"), ("cloth_red", "#8a3a2c"), ("cloth_green", "#4f6b3c"), ("cloth_ochre", "#b0863c"),
    ("cloth_grey", "#7a7a76"), ("cloth_cream", "#d6caa8"), ("cloth_dark", "#3a3430"), ("cloth_brown", "#6b5038"),
    # skin and hair
    ("skin_light", "#e0b994"), ("skin", "#c4946c"), ("skin_dark", "#8c6244"), ("hair_brown", "#4e3424"),
    ("hair_black", "#22201e"), ("hair_blond", "#b8955a"), ("hair_grey", "#9a9690"), ("eye", "#1c1a18"),
    # nature
    ("grass", "#6e8a44"), ("leaf", "#557a36"), ("leaf_dark", "#3f5e2c"), ("pine", "#2f4c32"),
    ("bark", "#5a4232"), ("flower_purple", "#7a5a96"), ("flower_white", "#e6e2d4"), ("herb", "#6a9450"),
    # fire, water, wolf
    ("ember", "#c4581e"), ("flame", "#f0b03c"), ("water", "#3e6a7e"), ("wolf", "#7c7a74"),
    ("wolf_light", "#b4ada0"), ("wolf_dark", "#4a4844"), ("blood_dark", "#4a1e1a"), ("bone", "#ddd4c0"),
    # guard and banner accents
    ("banner_red", "#7a2a26"), ("banner_gold", "#c09a46"), ("tabard", "#5a2e2a"), ("cloth_teal", "#3e6a64"),
]

assert len(COLOURS) <= CELLS * CELLS, "palette is full"
INDEX = {name: i for i, (name, _) in enumerate(COLOURS)}


def rgb(hexcode):
    h = hexcode.lstrip("#")
    return tuple(int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4))


def uv(name):
    """UV of the centre of a colour's cell. Row 0 is the top row of the PNG."""
    i = INDEX[name]
    col, row = i % CELLS, i // CELLS
    u = (col + 0.5) / CELLS
    v = 1.0 - (row + 0.5) / CELLS
    return (u, v)


def pixels():
    """Flat RGBA float list, bottom row first (Blender's image layout)."""
    out = []
    for y in range(SIZE):
        row = CELLS - 1 - (y // CELL_PX)          # bottom pixel rows are the last cell row
        for x in range(SIZE):
            i = row * CELLS + x // CELL_PX
            r, g, b = rgb(COLOURS[i][1]) if i < len(COLOURS) else (1.0, 0.0, 1.0)
            out += [r, g, b, 1.0]
    return out
