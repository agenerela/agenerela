"""Writes the GreyBoxStrategy (Sundermark) screen mockups as SVG, beside this file.

    python docs/demos/greyboxstrategy/mockups/make_mockups.py

One script draws all six so the map, the palette and the type stay the same on every screen.
The game state they show is one moment: spring of year 3 (docs/demos/greyboxstrategy.md,
"A season in pictures"). Standard library only.
"""

import math
import os
from xml.sax.saxutils import escape

HERE = os.path.dirname(os.path.abspath(__file__))
W, H = 1280, 720

# Palette ------------------------------------------------------------------------------
INK = "#2B2622"
INK2 = "#6B625A"
PARCH = "#F3EBDD"
PANEL = "#FBF7EF"
GOLD = "#B8954A"
SEA = "#9DB3BF"
SEA_DEEP = "#86A0AE"
OK = "#3F8F4F"
AMBER = "#C9821B"
BAD = "#B23A3A"
DARK = "#1E2328"
DARK2 = "#2A3138"
DARK_TX = "#D8DEE4"
DARK_MUTED = "#8C98A4"
MONO = "Consolas, 'Cascadia Mono', Menlo, monospace"
SANS = "'Segoe UI', 'Helvetica Neue', Arial, sans-serif"
SERIF = "Georgia, 'Times New Roman', serif"

REALMS = {
    "aster": ("Aster", "#3B6EA8", 288),
    "valen": ("Valen", "#A8452F", 0),
    "orm": ("Orm", "#2E8C85", 72),
    "kharsa": ("Kharsa", "#C8963A", 144),
    "thornwood": ("Thornwood", "#4F7A3A", 216),
}
CROWN = "#8F8A82"

# Province ids per realm sector: (counter-clockwise inner, clockwise inner, capital).
SECTORS = {
    "aster": ("bellmoor", "lark_hollow", "brightwater"),
    "valen": ("kestrel_pass", "high_barrow", "ironhold"),
    "orm": ("saltmere", "gull_reach", "tidegate"),
    "kharsa": ("cinder_flats", "redgrass", "ashkar"),
    "thornwood": ("duskwood", "mossfen", "elderwood"),
}

# Spring of year 3: owner and armies of every province, and which have walls or a market.
STATE = {
    "brightwater": ("aster", 4), "bellmoor": ("aster", 2), "lark_hollow": ("aster", 3),
    "ironhold": ("valen", 4), "kestrel_pass": ("valen", 3), "high_barrow": ("valen", 2),
    "tidegate": ("orm", 3), "saltmere": ("orm", 3), "gull_reach": ("kharsa", 3),
    "ashkar": ("kharsa", 3), "cinder_flats": ("kharsa", 4), "redgrass": ("kharsa", 2),
    "elderwood": ("thornwood", 3), "duskwood": ("thornwood", 2), "mossfen": ("thornwood", 3),
    "old_crown": (None, 3),
}
WALLS = {"high_barrow", "mossfen", "brightwater"}
MARKETS = {"tidegate", "saltmere", "bellmoor"}
CAPITALS = {"brightwater", "ironhold", "tidegate", "ashkar", "elderwood"}


def esc(s):
    return escape(str(s), {'"': "&quot;"})


def text(x, y, s, size=13, fill=INK, weight="normal", anchor="start", family=SANS, style="", extra=""):
    st = f' font-style="{style}"' if style else ""
    return (f'<text x="{x:.1f}" y="{y:.1f}" font-family="{family}" font-size="{size}" fill="{fill}" '
            f'font-weight="{weight}" text-anchor="{anchor}"{st}{extra}>{esc(s)}</text>')


def rect(x, y, w, h, fill, stroke="none", rx=0, sw=1, extra=""):
    return (f'<rect x="{x:.1f}" y="{y:.1f}" width="{w:.1f}" height="{h:.1f}" rx="{rx}" fill="{fill}" '
            f'stroke="{stroke}" stroke-width="{sw}"{extra}/>')


def line(x1, y1, x2, y2, stroke=GOLD, sw=1, extra=""):
    return f'<line x1="{x1:.1f}" y1="{y1:.1f}" x2="{x2:.1f}" y2="{y2:.1f}" stroke="{stroke}" stroke-width="{sw}"{extra}/>'


def circle(cx, cy, r, fill, stroke="none", sw=1, extra=""):
    return f'<circle cx="{cx:.1f}" cy="{cy:.1f}" r="{r:.1f}" fill="{fill}" stroke="{stroke}" stroke-width="{sw}"{extra}/>'


def pill(x, y, label, fill, tx="#fff", size=11, pad=7, h=18):
    w = len(label) * size * 0.58 + pad * 2
    return rect(x, y, w, h, fill, rx=h / 2) + text(x + w / 2, y + h * 0.72, label, size, tx, "600", "middle"), w


def svg(body, title):
    return (f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {W} {H}" width="{W}" height="{H}" '
            f'role="img" aria-label="{esc(title)}">\n<title>{esc(title)}</title>\n'
            f'{rect(0, 0, W, H, PARCH)}\n{body}\n</svg>\n')


def write(name, body, title):
    with open(os.path.join(HERE, name), "w", encoding="utf-8", newline="\n") as f:
        f.write(svg(body, title))


# The map --------------------------------------------------------------------------------
def pt(cx, cy, r, a):
    a = math.radians(a)
    return cx + r * math.sin(a), cy - r * math.cos(a)


def poly(points, fill, stroke="#FBF7EF", sw=2, extra=""):
    p = " ".join(f"{x:.1f},{y:.1f}" for x, y in points)
    return f'<polygon points="{p}" fill="{fill}" stroke="{stroke}" stroke-width="{sw}" stroke-linejoin="round"{extra}/>'


def centroid(points):
    xs = [p[0] for p in points]
    ys = [p[1] for p in points]
    return sum(xs) / len(xs), sum(ys) / len(ys)


def map_shapes(cx, cy, s):
    """Every province's polygon, keyed by id. s scales the whole map."""
    r1, r2, r3 = 62 * s, 150 * s, 250 * s
    shapes = {}
    bounds = [36, 108, 180, 252, 324]
    shapes["old_crown"] = [pt(cx, cy, r1, b) for b in bounds]
    for realm, (_, _, a) in REALMS.items():
        left, right, cap = SECTORS[realm]
        p_l = pt(cx, cy, r1, a - 36)
        p_r = pt(cx, cy, r1, a + 36)
        mid = ((p_l[0] + p_r[0]) / 2, (p_l[1] + p_r[1]) / 2)
        shapes[left] = [p_l, mid, pt(cx, cy, r2, a), pt(cx, cy, r2 * 0.97, a - 18), pt(cx, cy, r2, a - 36)]
        shapes[right] = [mid, p_r, pt(cx, cy, r2, a + 36), pt(cx, cy, r2 * 0.97, a + 18), pt(cx, cy, r2, a)]
        outer = [pt(cx, cy, r3 * k, a + d) for d, k in ((29, 0.93), (15, 1.0), (0, 1.03), (-15, 1.0), (-29, 0.93))]
        shapes[cap] = [pt(cx, cy, r2, a - 36), pt(cx, cy, r2 * 0.97, a - 18), pt(cx, cy, r2, a),
                       pt(cx, cy, r2 * 0.97, a + 18), pt(cx, cy, r2, a + 36)] + outer
    return shapes


def draw_map(cx, cy, s=1.0, labels=True, highlight=None, arrows=(), dim=False):
    out = []
    shapes = map_shapes(cx, cy, s)
    # sea glow
    out.append(circle(cx, cy, 285 * s, SEA_DEEP))
    out.append(circle(cx, cy, 268 * s, SEA))
    for pid, pts in shapes.items():
        owner, armies = STATE[pid]
        fill = REALMS[owner][1] if owner else CROWN
        op = ' fill-opacity="0.55"' if dim and pid != highlight else ""
        stroke, sw = ("#FFE9A8", 4) if pid == highlight else ("#FBF7EF", 2)
        out.append(poly(pts, fill, stroke, sw * s, op))
    for pid, pts in shapes.items():
        owner, armies = STATE[pid]
        x, y = centroid(pts)
        if pid in CAPITALS:
            # a small keep
            out.append(rect(x - 7 * s, y - 26 * s, 14 * s, 11 * s, "#FBF7EF", INK, 1, sw=1))
            out.append(rect(x - 9 * s, y - 29 * s, 4 * s, 4 * s, "#FBF7EF", INK, 0, sw=1))
            out.append(rect(x + 5 * s, y - 29 * s, 4 * s, 4 * s, "#FBF7EF", INK, 0, sw=1))
        if pid == "old_crown":
            out.append(f'<path d="M{x-14*s:.1f},{y-14*s:.1f} l5,-9 l4,6 l5,-10 l5,10 l4,-6 l5,9 z" fill="#E6C66A" stroke="{INK}" stroke-width="1"/>')
        if pid in WALLS:
            out.append(f'<path d="M{x+16*s:.1f},{y+4*s:.1f} h12 v-6 h-3 v3 h-2 v-3 h-2 v3 h-2 v-3 h-3 z" fill="#FBF7EF" stroke="{INK}" stroke-width="0.8"/>')
        if pid in MARKETS:
            out.append(f'<path d="M{x-28*s:.1f},{y+4*s:.1f} h11 v-6 l-5.5,-4 l-5.5,4 z" fill="#FFE2A0" stroke="{INK}" stroke-width="0.8"/>')
        if labels:
            out.append(text(x, y + 2 * s, pid, 10.5 * s if s < 1 else 11, "#FFFFFF", "600", "middle",
                            extra=' paint-order="stroke" stroke="#00000055" stroke-width="2.5"'))
        out.append(circle(x, y + 17 * s, 9 * s, "#FBF7EF", INK, 1))
        out.append(text(x, y + 21 * s, armies, 11 * s, INK, "700", "middle"))
    for (a, b, colour) in arrows:
        ax, ay = centroid(shapes[a])
        bx, by = centroid(shapes[b])
        bx, by = ax + (bx - ax) * 0.62, ay + (by - ay) * 0.62
        out.append(f'<path d="M{ax:.1f},{ay:.1f} Q{(ax+bx)/2+6:.1f},{(ay+by)/2-14:.1f} {bx:.1f},{by:.1f}" fill="none" '
                   f'stroke="{colour}" stroke-width="4" stroke-dasharray="8 5" marker-end="url(#arrow)"/>')
    return "\n".join(out)


DEFS = (f'<defs><marker id="arrow" viewBox="0 0 10 10" refX="7" refY="5" markerWidth="5" markerHeight="5" orient="auto">'
        f'<path d="M0,0 L10,5 L0,10 z" fill="{INK}"/></marker>'
        f'<filter id="shadow" x="-5%" y="-5%" width="110%" height="115%"><feDropShadow dx="0" dy="2" stdDeviation="3" flood-opacity="0.18"/></filter></defs>')


def panel(x, y, w, h, title=None, fill=PANEL):
    out = [rect(x, y, w, h, fill, GOLD, 6, 1, ' filter="url(#shadow)"')]
    if title:
        out.append(text(x + 14, y + 25, title, 15, INK, "700", family=SERIF))
        out.append(line(x + 12, y + 34, x + w - 12, y + 34, GOLD, 0.8))
    return "\n".join(out)


def top_bar(season="Spring, Year 3", phase="Council", button="End Season"):
    out = [rect(0, 0, W, 46, PANEL, GOLD, 0, 1)]
    out.append(rect(12, 8, 30, 30, REALMS["aster"][1], rx=4))
    out.append(text(27, 29, "A", 16, "#fff", "700", "middle", SERIF))
    out.append(text(52, 29, "Aster", 17, INK, "700", family=SERIF))
    items = [("Gold", "85  (+24)"), ("Armies", "9"), ("Renown", "7 / 20"), ("Envoys", "2 left")]
    x = 150
    for k, v in items:
        out.append(text(x, 20, k.upper(), 9, INK2, "600"))
        out.append(text(x, 37, v, 15, INK, "700"))
        x += 120
    out.append(text(650, 20, "SEASON", 9, INK2, "600"))
    out.append(text(650, 37, season, 15, INK, "700"))
    p, w = pill(800, 15, phase + " phase", INK, size=11)
    out.append(p)
    out.append(rect(W - 160, 8, 148, 30, BAD, rx=5))
    out.append(text(W - 86, 28, button + "  ›", 14, "#fff", "700", "middle"))
    return "\n".join(out)


def seal(x, y, colour, state):
    """A realm's deliberation seal: 'done', 'thinking', 'queued' or 'idle'."""
    out = [circle(x, y, 11, colour if state != "idle" else "#D9D2C5", "#00000033", 1)]
    if state == "thinking":
        out.append(f'<circle cx="{x:.1f}" cy="{y:.1f}" r="15" fill="none" stroke="{colour}" stroke-width="2.5" stroke-dasharray="20 74"/>')
    if state == "done":
        out.append(f'<path d="M{x-5:.1f},{y:.1f} l3.5,4 l6.5,-8" fill="none" stroke="#fff" stroke-width="2.4"/>')
    if state == "queued":
        out.append(text(x, y + 4, "…", 12, "#fff", "700", "middle"))
    return "\n".join(out)


# Screen 1 — the map ---------------------------------------------------------------------
def screen_map():
    b = [DEFS, top_bar()]
    b.append(rect(250, 46, 770, 674, SEA))
    b.append(draw_map(635, 372, 1.08, arrows=[("lark_hollow", "old_crown", "#3B6EA8")]))
    # left: rival realms
    b.append(panel(12, 58, 226, 520, "Rival realms"))
    rows = [
        ("valen", "peace", OK, "done", "Raised levies"),
        ("orm", "peace", OK, "thinking", "Deliberating…"),
        ("kharsa", "peace", OK, "queued", "Waiting · 2 ahead"),
        ("thornwood", "allied", "#3B6EA8", "queued", "Waiting · 3 ahead"),
    ]
    y = 102
    for rid, rel, rc, st, line2 in rows:
        name, col, _ = REALMS[rid]
        b.append(rect(22, y - 10, 206, 104, "#FFFFFF", "#E4DACA", 5))
        b.append(rect(22, y - 10, 6, 104, col, rx=2))
        b.append(text(38, y + 10, name, 15, INK, "700", family=SERIF))
        b.append(seal(208, y + 6, col, st))
        p, _ = pill(38, y + 20, rel, rc, size=10, h=16)
        b.append(p)
        b.append(text(38, y + 55, "Gold " + {"valen": "62", "orm": "140", "kharsa": "40", "thornwood": "55"}[rid]
                      + " · Armies " + {"valen": "9", "orm": "6", "kharsa": "12", "thornwood": "8"}[rid], 11, INK2))
        b.append(text(38, y + 75, line2, 11, INK, "600", style="italic"))
        y += 118
    b.append(panel(12, 590, 226, 118, "The council queue"))
    b.append(text(24, 645, "4 councils · 1 letter in flight", 11, INK2))
    bars = [(REALMS["valen"][1], 24, 40, "valen"), ("#3B6EA8", 66, 30, "letter→orm"),
            (REALMS["orm"][1], 98, 46, "orm"), (REALMS["kharsa"][1], 146, 40, ""), (REALMS["thornwood"][1], 188, 38, "")]
    for col, x, w, lab in bars:
        b.append(rect(x, 656, w - 3, 16, col, rx=3, extra=' fill-opacity="0.9"'))
    b.append(text(24, 694, "Your letter jumped the queue (player-facing).", 10.5, INK, style="italic"))
    # right: chronicle
    b.append(panel(1032, 58, 236, 430, "Chronicle"))
    entries = [
        ("valen", "Winter, Y2", "Valen fortified high_barrow. “Let orm break its teeth on our walls.”"),
        ("kharsa", "Winter, Y2", "Kharsa took gull_reach from orm in battle (4 against 3)."),
        ("orm", "Winter, Y2", "Orm refused valen’s offer of peace."),
        ("thornwood", "Winter, Y2", "Thornwood kept its counsel."),
        ("valen", "Spring, Y3", "Valen raised two levies at ironhold."),
    ]
    y = 112
    for rid, when, s in entries:
        col = REALMS[rid][1]
        b.append(circle(1048, y - 4, 7, col))
        b.append(text(1062, y, when, 10, INK2, "600"))
        words, cur, lines = s.split(), "", []
        for w_ in words:
            if len(cur) + len(w_) > 31:
                lines.append(cur)
                cur = w_
            else:
                cur = (cur + " " + w_).strip()
        lines.append(cur)
        for i, ln in enumerate(lines):
            b.append(text(1062, y + 16 + i * 15, ln, 11.5, INK))
        y += 26 + len(lines) * 15
    b.append(rect(1044, y - 6, 212, 40, "#FFF4DE", AMBER, 4))
    b.append(text(1052, y + 9, "Contained: valen chose to declare", 10.5, INK, "600"))
    b.append(text(1052, y + 24, "war on aster — a truce holds.", 10.5, INK, "600"))
    # right bottom: selected province
    b.append(panel(1032, 500, 236, 208, "lark_hollow"))
    b.append(text(1046, 555, "Aster · 3 armies · no walls", 12, INK))
    b.append(text(1046, 575, "Borders: bellmoor, brightwater,", 11, INK2))
    b.append(text(1046, 590, "kestrel_pass, old_crown", 11, INK2))
    for i, (lab, col) in enumerate([("March on old_crown", BAD), ("Build walls · 20", INK), ("Raise levies · 30", INK)]):
        b.append(rect(1046, 604 + i * 32, 208, 26, "#FFFFFF" if col == INK else "#F7E3DE", col, 4))
        b.append(text(1150, 621 + i * 32, lab, 12, col, "600", "middle"))
    # bottom drawer
    b.append(rect(262, 650, 746, 58, PANEL, GOLD, 6, 1, ' filter="url(#shadow)"'))
    b.append(text(276, 672, "Envoy desk", 13, INK, "700", family=SERIF))
    b.append(text(276, 692, "To orm · terms: alliance against kharsa", 11.5, INK2))
    b.append(rect(560, 662, 300, 34, "#FFFFFF", "#E4DACA", 4))
    b.append(text(570, 684, "“Your harbours burn while kharsa grows fat…”", 11.5, INK, style="italic"))
    b.append(rect(872, 664, 124, 30, REALMS["aster"][1], rx=5))
    b.append(text(934, 684, "Seal and send", 12.5, "#fff", "700", "middle"))
    b.append(text(642, 76, "MAP · spring of year 3", 11, "#2B262299", "700", "middle"))
    write("01-map.svg", "\n".join(b), "Sundermark main screen: the map during the council phase")


# Screen 2 — the realm dossier -----------------------------------------------------------
def screen_realm():
    b = [DEFS, top_bar()]
    b.append(rect(0, 46, 430, 674, SEA))
    b.append(draw_map(215, 380, 0.78, labels=False, highlight="ironhold", dim=True))
    x0 = 440
    b.append(panel(x0, 58, 828, 650))
    col = REALMS["valen"][1]
    b.append(rect(x0, 58, 828, 64, col, rx=6))
    b.append(text(x0 + 20, 90, "The Iron Margravate of Valen", 22, "#fff", "700", family=SERIF))
    b.append(text(x0 + 20, 110, "Margravine Isolde · proud, militarist, keeps her word to the letter", 12.5, "#FFFFFFDD", style="italic"))
    p, _ = pill(x0 + 690, 80, "qwen3.5:2b · Ollama", "#00000055", size=11)
    b.append(p)
    # stats row
    stats = [("Gold", "62", "+20 a season"), ("Armies", "9", "ironhold 4 · kestrel_pass 3 · high_barrow 2"),
             ("Provinces", "3", "high_barrow walled"), ("Renown", "9", "2nd of 5")]
    x = x0 + 20
    for k, v, sub in stats:
        b.append(text(x, 148, k.upper(), 9.5, INK2, "700"))
        b.append(text(x, 174, v, 24, INK, "700", family=SERIF))
        b.append(text(x, 192, sub, 10.5, INK2))
        x += 200
    # relations
    b.append(text(x0 + 20, 226, "Relations", 14, INK, "700", family=SERIF))
    rels = [("aster", "peace · truce until summer", OK), ("orm", "war since autumn, Y1", BAD),
            ("kharsa", "peace", OK), ("thornwood", "peace · wary", OK)]
    y = 246
    for rid, s, c in rels:
        b.append(rect(x0 + 20, y - 12, 10, 10, REALMS[rid][1], rx=2))
        b.append(text(x0 + 38, y - 3, rid, 12, INK, "600"))
        b.append(text(x0 + 120, y - 3, s, 12, c))
        y += 20
    b.append(text(x0 + 20, 344, "Ledger (game-verified, never the model’s)", 14, INK, "700", family=SERIF))
    for i, s in enumerate(["Autumn Y1 — orm broke its pact with valen.", "Summer Y2 — aster sent valen 20 gold.",
                           "Winter Y2 — orm refused valen’s offer of peace."]):
        b.append(text(x0 + 20, 366 + i * 18, "• " + s, 12, INK))
    # what it is told
    bx = x0 + 420
    b.append(rect(bx, 214, 390, 240, "#FFFFFF", "#E4DACA", 5))
    b.append(text(bx + 14, 236, "What valen is told this season", 13.5, INK, "700", family=SERIF))
    obs = ["Spring of year 3. 21 seasons remain.", "Treasury: 62 gold, income 20 a season.",
           "Armies: ironhold 4, kestrel_pass 3, high_barrow 2.", "high_barrow has walls.",
           "At war with orm. At peace with aster, kharsa, thornwood.",
           "orm holds saltmere beside high_barrow, with 3 armies.",
           "old_crown is unclaimed, held by 3 pretender armies.",
           "Long memory: orm broke its pact with you in autumn of year 1.",
           "Renown: orm 11, valen 9, aster 7, kharsa 6, thornwood 4."]
    for i, s in enumerate(obs):
        b.append(text(bx + 14, 258 + i * 19, "- " + s, 11, INK, family=MONO))
    b.append(text(bx + 14, 430, "Chancellor: “orm’s armies gather at saltmere,", 11, "#7A2E1F", "700", family=MONO))
    b.append(text(bx + 14, 444, "             beside high_barrow.”", 11, "#7A2E1F", "700", family=MONO, extra=' xml:space="preserve"'))
    # what it may do
    b.append(text(x0 + 20, 470, "What valen may do now", 14, INK, "700", family=SERIF))
    b.append(text(x0 + 200, 470, "offered actions go in the schema; masked ones are absent, not forbidden in prose", 11, INK2, style="italic"))
    acts = [("raise_levies", True, "no target", "chosen"), ("march_on", True, "saltmere, old_crown", ""),
            ("fortify", True, "ironhold, kestrel_pass", ""), ("build_market", True, "own provinces", ""),
            ("declare_war", True, "aster, kharsa, thornwood", ""), ("offer_peace", True, "orm", ""),
            ("propose_alliance", True, "aster, kharsa, thornwood", ""), ("send_gift", True, "any realm", ""),
            ("demand_tribute", False, "no realm has under 6 armies", ""),
            ("accept_terms", False, "no envoy is waiting", ""), ("reject_terms", False, "no envoy is waiting", ""),
            ("none", True, "always, listed last", "")]
    for i, (a, on, why, tag) in enumerate(acts):
        cx_ = x0 + 20 + (i % 2) * 404
        cy_ = 488 + (i // 2) * 34
        fill = "#EEF6EF" if on else "#F1EEEA"
        stroke = OK if tag == "chosen" else ("#CFE3D2" if on else "#E2DCD3")
        b.append(rect(cx_, cy_, 392, 28, fill, stroke, 4, 2 if tag else 1))
        b.append(text(cx_ + 12, cy_ + 19, a, 12.5, INK if on else "#A39A90", "700", family=MONO,
                      extra='' if on else ' text-decoration="line-through"'))
        b.append(text(cx_ + 150, cy_ + 19, why, 11, INK2 if on else "#A39A90", style="" if on else "italic"))
        if tag:
            p, _ = pill(cx_ + 330, cy_ + 5, "chosen", OK, size=10, h=18)
            b.append(p)
    write("02-realm.svg", "\n".join(b), "Sundermark realm dossier: what Valen is told and what it may do")


# Screen 3 — the envoy desk --------------------------------------------------------------
def screen_envoy():
    b = [DEFS, top_bar()]
    b.append(rect(0, 46, W, 674, "#E9DFCC"))
    # left: compose
    b.append(panel(24, 66, 600, 420, "Write to a realm"))
    b.append(text(40, 128, "To", 11, INK2, "700"))
    for i, rid in enumerate(["valen", "orm", "kharsa", "thornwood"]):
        name, col, _ = REALMS[rid]
        sel = rid == "orm"
        b.append(rect(70 + i * 128, 112, 118, 28, col if sel else "#FFFFFF", col, 5, 2))
        b.append(text(129 + i * 128, 131, name, 12.5, "#fff" if sel else col, "700", "middle"))
    b.append(text(40, 172, "Terms", 11, INK2, "700"))
    terms = ["Alliance", "Peace", "Pact", "Tribute", "No terms"]
    for i, t in enumerate(terms):
        sel = t == "No terms"
        b.append(rect(90 + i * 104, 156, 96, 26, INK if sel else "#FFFFFF", INK, 13, 1))
        b.append(text(138 + i * 104, 173, t, 12, "#fff" if sel else INK, "600", "middle"))
    b.append(text(40, 208, "Letter", 11, INK2, "700"))
    b.append(rect(40, 216, 568, 150, "#FFFFFF", "#E4DACA", 4))
    letter = ["Harbour Master Benedek,", "", "Join us against the Empire of Thule. Their fleets",
              "sit fat with silver, and together we would split it", "evenly. Say the word and our banners sail with yours.", "",
              "— Aster"]
    for i, s in enumerate(letter):
        b.append(text(56, 240 + i * 18, s, 13, INK, family=SERIF, style="italic"))
    b.append(text(40, 392, "One of your 2 envoys this season · Orm reads the letter, its terms, and what it", 11, INK2))
    b.append(text(40, 408, "already knows. It answers with one of its legal moves, and a letter back.", 11, INK2))
    b.append(rect(460, 428, 148, 36, REALMS["aster"][1], rx=6))
    b.append(text(534, 451, "Seal and send", 13.5, "#fff", "700", "middle"))
    # right: reply
    b.append(panel(648, 66, 608, 420, "Reply from Orm"))
    b.append(rect(664, 112, 576, 196, "#FFFDF8", "#E4DACA", 4))
    reply = ["To the court of Aster,", "", "We have charted every shore from here to the", "sunrise, and no map of ours names a Thule. Orm",
             "trades in silver, not in ghosts. Write again when", "your enemies are real.", "", "— Benedek of Orm"]
    for i, s in enumerate(reply):
        b.append(text(684, 136 + i * 19, s, 13, INK, family=SERIF, style="italic"))
    b.append(rect(664, 320, 576, 52, "#FFF4DE", AMBER, 5))
    b.append(text(680, 342, "Contained by the target check", 13, "#7A4A0A", "700"))
    b.append(text(680, 360, "Orm reached for “thule”; only 4% of its choice stayed on the list → no action.", 11.5, INK))
    b.append(rect(664, 384, 180, 30, "#FFFFFF", INK, 5))
    b.append(text(754, 404, "Why did Orm do that?", 12, INK, "700", "middle"))
    b.append(text(860, 404, "opens the inspector on this decision", 11, INK2, style="italic"))
    # bottom: incoming proposal card
    b.append(panel(24, 506, 1232, 196, "A proposal waits for your word"))
    col = REALMS["valen"][1]
    b.append(rect(40, 552, 8, 132, col, rx=2))
    b.append(text(62, 572, "Valen proposes an alliance", 16, INK, "700", family=SERIF))
    b.append(text(62, 596, "“Orm’s ships raid us both. Stand with Valen until orm kneels, and ironhold’s", 13, INK, family=SERIF, style="italic"))
    b.append(text(62, 614, "smiths will remember the friend who came when called.”", 13, INK, family=SERIF, style="italic"))
    b.append(text(62, 642, "If you accept: allied with valen; at war with orm; your truce with valen becomes an alliance.", 11.5, INK2))
    b.append(text(62, 660, "Checked again when you click: valen must still be at war with orm, and you at peace with valen.", 11.5, INK2))
    b.append(rect(1010, 584, 106, 34, OK, rx=6))
    b.append(text(1063, 606, "Accept", 13.5, "#fff", "700", "middle"))
    b.append(rect(1126, 584, 106, 34, "#FFFFFF", BAD, 6, 1.5))
    b.append(text(1179, 606, "Decline", 13.5, BAD, "700", "middle"))
    b.append(text(1010, 642, "Expires at the end of the season", 11, INK2))
    write("03-envoy.svg", "\n".join(b), "Sundermark envoy desk: a letter to Orm, its reply, and Valen's proposal")


# Screen 4 — the chronicle ---------------------------------------------------------------
def screen_chronicle():
    b = [DEFS, top_bar(phase="Chronicle", button="Next Season")]
    b.append(rect(0, 46, W, 674, "#E9DFCC"))
    b.append(panel(150, 62, 980, 640))
    b.append(text(640, 104, "The Chronicle of Sundermark", 26, INK, "700", "middle", SERIF))
    b.append(text(640, 126, "Year 2 · every move, every letter, every refusal", 12.5, INK2, anchor="middle", style="italic"))
    # filters
    fx = 186
    for lab, on in [("All", True), ("Battles", False), ("Letters", False), ("Proclamations", False), ("Contained", False)]:
        p, w = pill(fx, 140, lab, INK if on else "#D9D2C5", "#fff" if on else INK, size=11, h=20)
        b.append(p)
        fx += w + 8
    seasons = [
        ("Autumn, Year 2", [
            ("orm", "letter", "Orm to valen: “Your iron rusts; our silver does not. Pay for your peace.”", None),
            ("valen", "move", "Valen rejected the terms. “We do not buy what we can hold.”", None),
            ("kharsa", "move", "Kharsa raised two levies at ashkar.", None),
            ("aster", "you", "You sent thornwood 20 gold.", None),
        ]),
        ("Winter, Year 2", [
            ("valen", "move", "Valen fortified high_barrow. “Let orm break its teeth on our walls.”", None),
            ("kharsa", "battle", "Kharsa marched on gull_reach: 4 against 3, the dice 5 and 2. gull_reach falls to kharsa.", None),
            ("orm", "letter", "Orm refused valen’s offer of peace.", None),
            ("valen", "contained", "Valen chose to declare war on aster. Truce guard: the truce holds until summer of year 3.", "TruceGuard"),
            ("thornwood", "move", "Thornwood kept its counsel.", None),
            ("kharsa", "refused", "Kharsa’s levies never came: the treasury was empty when the season turned.", "Execute"),
        ]),
    ]
    y = 186
    for title, ents in seasons:
        b.append(text(186, y, title, 15, INK, "700", family=SERIF))
        b.append(line(186, y + 8, 1094, y + 8, GOLD, 0.8))
        y += 30
        for rid, kind, s, tag in ents:
            col = REALMS[rid][1]
            if tag:
                b.append(rect(180, y - 15, 920, 34, "#FFF4DE" if tag == "TruceGuard" else "#F7E3DE", AMBER if tag == "TruceGuard" else BAD, 4))
            b.append(circle(200, y - 3, 7, col))
            b.append(text(216, y + 1, kind.upper(), 9.5, INK2, "700"))
            b.append(text(300, y + 1, s, 12.5, INK, family=SERIF))
            if tag:
                p, _ = pill(1000, y - 11, tag, AMBER if tag == "TruceGuard" else BAD, size=10)
                b.append(p)
            y += 36
        y += 12
    b.append(text(640, 690, "Amber: a guard stopped a legal but wrong move (contained). Red: the game refused a move at execution.", 11.5, INK2, anchor="middle", style="italic"))
    write("04-chronicle.svg", "\n".join(b), "Sundermark chronicle: a year of moves, letters and contained refusals")


# Screen 5 — the inspector ---------------------------------------------------------------
def mono_block(b, x, y, w, title, lines, colour=DARK_TX, title_colour=DARK_MUTED, lh=15, size=11):
    h = 28 + lh * len(lines)
    b.append(rect(x, y, w, h, DARK2, "#3A434C", 5))
    b.append(text(x + 10, y + 17, title, 10, title_colour, "700"))
    for i, ln in enumerate(lines):
        c = colour
        if isinstance(ln, tuple):
            ln, c = ln
        b.append(text(x + 10, y + 34 + i * lh, ln, size, c, family=MONO, extra=' xml:space="preserve"'))
    return y + h + 10


def screen_inspector():
    b = [DEFS]
    b.append(rect(0, 0, W, H, DARK))
    b.append(rect(0, 0, W, 50, "#161A1E"))
    b.append(text(20, 32, "Why did Orm do that?", 19, "#FFFFFF", "700", family=SERIF))
    b.append(text(250, 32, "Envoy from aster · spring of year 3 · decision 41 of the game", 12.5, DARK_MUTED))
    for i, (lab, col) in enumerate([("Capture as prompt", "#3B6EA8"), ("Replay on qwen3.5:4b", "#4A545E"), ("Copy request JSON", "#4A545E")]):
        b.append(rect(820 + i * 150, 12, 142, 28, col, rx=5))
        b.append(text(891 + i * 150, 31, lab, 11.5, "#fff", "600", "middle"))
    # pipeline strip
    steps = [("1 Asked", OK), ("2 Gathered", OK), ("3 Offered", OK), ("4 Prompt", OK), ("5 Queue", OK),
             ("6 Model", OK), ("7 Guards", AMBER), ("8 Executed", OK)]
    for i, (s, c) in enumerate(steps):
        x = 20 + i * 155
        b.append(rect(x, 62, 145, 26, c, rx=13, extra=' fill-opacity="0.22"'))
        b.append(rect(x, 62, 145, 26, "none", c, 13, 1.5))
        b.append(text(x + 72, 80, s, 12, "#FFFFFF", "700", "middle"))
    # column 1
    y = 102
    y = mono_block(b, 20, y, 400, "STIMULUS  · label “Envoy from aster” · priority PlayerFacing", [
        "Envoy from aster: \"Join us against the Empire of", " Thule. Their fleets sit fat with silver, and", " together we would split it evenly. ...\""])
    y = mono_block(b, 20, y, 400, "OBSERVATIONS  · 8 sent, 0 dropped (cap 10)", [
        "- The envoy brings no terms.", "- Treasury: 140 gold, income 30 a season.",
        "- Armies: tidegate 3, saltmere 3.", "- At war with valen, kharsa. At peace with", "  aster, thornwood.",
        "- Last season: kharsa took gull_reach from you.", "- Your trust in aster: fair. No broken word.",
        "- Renown: orm 11, valen 9, aster 7, ..."])
    y = mono_block(b, 20, y, 400, "HISTORY  · RollingHistory, 2 exchanges sent, 4 dropped", [
        "Chancellor: \"kharsa's riders are at gull_reach.\"", ("{\"action\":\"raise_levies\",\"target\":\"no_target\",...}", "#9FD3A7")])
    # column 2: schema
    x2 = 440
    y2 = 102
    y2 = mono_block(b, x2, y2, 400, "OFFERED ACTIONS  · 5 of 12 · availability from state", [
        ("declare_war        realm", DARK_TX), ("offer_peace        realm", DARK_TX), ("propose_alliance   realm", DARK_TX),
        ("send_gift          realm", DARK_TX), ("raise_levies       — council only", "#6F7A85"),
        ("accept_terms       — no terms brought", "#6F7A85"), ("none               (last)", DARK_TX)])
    y2 = mono_block(b, x2, y2, 400, "TARGETS  · enumerated in the schema, listed in the prompt", [
        "aster       realm   The Free Cities of Aster", "valen       realm   at war with you",
        "kharsa      realm   at war with you", "thornwood   realm   at peace with you",
        ("no_target", "#6F7A85")])
    y2 = mono_block(b, x2, y2, 400, "MODEL REPLY  · qwen3.5:2b · 0.47 s · 412 → 61 tokens", [
        "{", "  \"action\": \"declare_war\",", ("  \"target\": \"thornwood\",", "#F2B27A"),
        "  \"statement\": \"Orm will sail against Thule at", "    your side, friend of Aster.\"", "}"])
    # column 3: guards
    x3 = 860
    b.append(rect(x3, 102, 400, 330, DARK2, "#3A434C", 5))
    b.append(text(x3 + 10, 119, "GUARDS  · in order; the first not to pass stops the rest", 10, DARK_MUTED, "700"))
    rows = [("SchemaLegalityGuard", "pass", OK, "declare_war offered; thornwood is a realm"),
            ("TargetGroundingGuard", "rewrite → none", AMBER, "lowest on-list share 0.04 < 0.80"),
            ("TargetCategoryGuard", "not run", "#6F7A85", ""),
            ("TruceGuard (game)", "not run", "#6F7A85", ""),
            ("RepeatProposalGuard (game)", "not run", "#6F7A85", "")]
    for i, (g, v, c, why) in enumerate(rows):
        yy = 142 + i * 40
        b.append(circle(x3 + 18, yy + 4, 6, c))
        b.append(text(x3 + 32, yy + 8, g, 12, DARK_TX, "700", family=MONO))
        b.append(text(x3 + 390, yy + 8, v, 11.5, c, "700", "end"))
        if why:
            b.append(text(x3 + 32, yy + 24, why, 10.5, DARK_MUTED))
    # grounding bar
    b.append(text(x3 + 12, 352, "Where the model’s probability went, token by token", 10.5, DARK_MUTED, "700"))
    toks = [("“th”", 0.98), ("“orn”", 0.04), ("“wood”", 0.99)]
    for i, (t, v) in enumerate(toks):
        bx = x3 + 12 + i * 128
        b.append(rect(bx, 362, 118, 14, "#3A434C", rx=3))
        b.append(rect(bx, 362, 118 * v, 14, OK if v >= 0.8 else AMBER, rx=3))
        b.append(text(bx, 392, f"{t} {v:.2f} on list", 10.5, DARK_TX, family=MONO))
    b.append(text(x3 + 12, 416, "At “orn”, 96% wanted “ule”: no realm named thule exists.", 10.5, "#F2B27A"))
    # final
    b.append(rect(x3, 442, 400, 156, DARK2, "#3A434C", 5))
    b.append(text(x3 + 10, 459, "WHAT HAPPENED", 10, DARK_MUTED, "700"))
    b.append(text(x3 + 12, 482, "model:  declare_war → thornwood", 12, "#F2B27A", family=MONO, extra=' text-decoration="line-through"'))
    b.append(text(x3 + 12, 502, "final:  none · no_target", 12, "#9FD3A7", "700", family=MONO))
    b.append(text(x3 + 12, 524, "statement: refusal line (AskForRefusalLine, 0.31 s)", 11, DARK_MUTED))
    b.append(text(x3 + 12, 542, "“...no map of ours names a Thule.”", 11.5, DARK_TX, family=SERIF, style="italic"))
    b.append(text(x3 + 12, 566, "Execute: NoAction · memory: committed (OnExecute)", 11, DARK_TX))
    b.append(text(x3 + 12, 584, "Outcome class: contained by guard", 11, "#F2C66D", "700"))
    # telemetry footer
    b.append(rect(20, 640, 1240, 64, "#161A1E", "#3A434C", 5))
    tel = [("latency", "0.47 s"), ("queue wait", "0.00 s (jumped 3)"), ("backlog", "0.00 s"), ("prompt", "412 tok"),
           ("history", "2 sent · 4 dropped"), ("provider", "OrmOllama · qwen3.5:2b"), ("framework", "0.4.x")]
    for i, (k, v) in enumerate(tel):
        b.append(text(36 + i * 176, 664, k.upper(), 9.5, DARK_MUTED, "700"))
        b.append(text(36 + i * 176, 686, v, 12.5, DARK_TX, "600", family=MONO))
    write("05-inspector.svg", "\n".join(b), "Sundermark inspector: why Orm's decision became no action")


# Screen 6 — the model league ------------------------------------------------------------
def screen_league():
    b = [DEFS, top_bar(season="Autumn, Year 4", phase="Spectator", button="Pause League")]
    b.append(rect(0, 46, 470, 674, SEA))
    b.append(draw_map(235, 360, 0.82, labels=False))
    b.append(panel(484, 58, 784, 380, "Model League · seed 2207 · same rules, same prompts, five brains"))
    hdr = ["Realm", "Brain", "Decisions", "Executed", "Contained", "Refused", "Errors", "Median", "Renown"]
    xs = [500, 610, 790, 870, 945, 1025, 1090, 1150, 1222]
    for h_, x in zip(hdr, xs):
        b.append(text(x, 120, h_.upper(), 9.5, INK2, "700", "end" if x > 760 else "start"))
    rows = [("aster", "qwen3.5:2b, no target check", 70, 61, 0, 2, 0, "0.43 s", 9),
            ("valen", "qwen3.5:4b", 71, 58, 6, 1, 0, "0.61 s", 14),
            ("orm", "qwen3.5:2b", 74, 55, 11, 2, 1, "0.44 s", 11),
            ("kharsa", "gemma4:e2b", 69, 57, 5, 3, 0, "0.83 s", 12),
            ("thornwood", "Steward (scripted)", 62, 62, 0, 0, 0, "0.00 s", 8)]
    for i, r in enumerate(rows):
        y = 146 + i * 34
        rid = r[0]
        b.append(rect(494, y - 18, 764, 30, "#FFFFFF" if i % 2 == 0 else "#FBF7EF", rx=3))
        b.append(rect(500, y - 10, 12, 12, REALMS[rid][1], rx=2))
        b.append(text(518, y + 1, REALMS[rid][0], 13, INK, "700"))
        b.append(text(610, y + 1, r[1], 12, INK, family=MONO))
        for v, x in zip(r[2:], xs[2:]):
            b.append(text(x, y + 1, v, 13, AMBER if x == 945 and v else INK, "600", "end"))
    b.append(text(500, 318, "A game result is an anecdote, not a measurement: it varies with the seed and the season. Accuracy", 11.5, INK2, style="italic"))
    b.append(text(500, 334, "claims come only from EvalRunner A/B runs on the captured prompt set (hard rule 2).", 11.5, INK2, style="italic"))
    b.append(rect(500, 352, 230, 30, "#FFFFFF", INK, 5))
    b.append(text(615, 372, "Swap a realm’s brain…", 12.5, INK, "700", "middle"))
    b.append(rect(742, 352, 250, 30, "#FFFFFF", INK, 5))
    b.append(text(867, 372, "Export captured prompts (212)", 12.5, INK, "700", "middle"))
    b.append(rect(1004, 352, 250, 30, REALMS["aster"][1], rx=5))
    b.append(text(1129, 372, "Run 10 seeds headless", 12.5, "#fff", "700", "middle"))
    # queue timeline
    b.append(panel(484, 452, 784, 256, "This season through the queue · one request at a time, background priority"))
    lanes = [("valen", [(0, 0.62, "council")]), ("orm", [(0.62, 0.45, "council"), (2.95, 0.43, "envoy from kharsa")]),
             ("kharsa", [(1.07, 0.84, "council")]), ("thornwood", [(1.91, 0.02, "council · scripted")]),
             ("aster", [(1.93, 0.52, "council"), (2.45, 0.5, "envoy from valen")])]
    scale = 170
    for i, (rid, segs) in enumerate(lanes):
        y = 510 + i * 34
        b.append(text(500, y + 4, rid, 12, INK, "700"))
        b.append(line(580, y, 1250, y, "#E4DACA", 1))
        col = REALMS[rid][1]
        for start, dur, lab in segs:
            x = 580 + start * scale
            w = max(dur * scale, 6)
            b.append(rect(x, y - 10, w, 20, col, rx=4, extra=' fill-opacity="0.9"'))
            b.append(text(x + w + 6, y + 4, lab, 10.5, INK2))
    for t in range(0, 5):
        x = 580 + t * scale
        b.append(text(x, 688, f"{t} s", 10, INK2, anchor="middle"))
    write("06-league.svg", "\n".join(b), "Sundermark Model League: four models play one seeded game")


if __name__ == "__main__":
    screen_map()
    screen_realm()
    screen_envoy()
    screen_chronicle()
    screen_inspector()
    screen_league()
    print("wrote 6 mockups to", HERE)
