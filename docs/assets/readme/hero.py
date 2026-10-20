# Writes hero.svg: the README banner. hero-bg.jpg (the painted scene with the wordmark, no guard)
# and guard.png (the guard cut out of the same painting) are embedded, and the agent's one decision
# plays over them on a loop: it hears, sees, remembers, thinks, acts, then walks to the tower.
# Run it after changing either image or the timings below.
import base64, os

HERE = os.path.dirname(os.path.abspath(__file__))
T = 16.0  # seconds per loop


def pct(t):
    return f"{100 * t / T:.2f}%"


def appear(name, t, dur=0.45, start="opacity:0;transform:translateY(6px)", end="opacity:1;transform:none"):
    """Hidden until t, then eases in and stays until the loop's fade."""
    return f"@keyframes {name}{{0%,{pct(t)}{{{start}}}{pct(t + dur)},100%{{{end}}}}}"


def appear_until(name, t, out, dur=0.45, start="opacity:0;transform:translateY(6px)", end="opacity:1;transform:none"):
    """Eases in at t and out again at out, before the guard walks through where it stood."""
    return (f"@keyframes {name}{{0%,{pct(t)}{{{start}}}{pct(t + dur)},{pct(out)}{{{end}}}"
            f"{pct(out + 0.4)},100%{{opacity:0}}}}")


# When each beat starts, in seconds.
HEAR, SEE, RECALL, THINK, MENU, MASK, PICK, PATH, DONE = 0.6, 1.9, 3.1, 4.2, 5.4, 6.4, 7.6, 8.1, 9.2
CLEAR, WALK, ARRIVE, FADE = 10.2, 10.5, 13.3, 14.0

# Where the guard's feet are, where they end up on the bridge, and how much smaller he is there.
FEET, DEST, FAR = (939, 472), (1080, 415), 0.8
STEP = 0.35  # seconds per step

bg = base64.b64encode(open(os.path.join(HERE, "hero-bg.jpg"), "rb").read()).decode()
guard = base64.b64encode(open(os.path.join(HERE, "guard.png"), "rb").read()).decode()

ARRIVED = f"translate({DEST[0] - FEET[0]}px,{DEST[1] - FEET[1]}px) scale({FAR})"
# Each step plants a foot (a slight lean one way, then the other) with a lift between.
bob = [f"0%,{pct(WALK)}{{transform:none}}"]
n = int((ARRIVE - WALK) / STEP)
for i in range(1, n):
    t = WALK + i * STEP
    bob.append(f"{pct(t - STEP / 2)}{{transform:translateY(-5px)}}")
    bob.append(f"{pct(t)}{{transform:rotate({1.4 if i % 2 else -1.4}deg)}}")
bob.append(f"{pct(ARRIVE)},100%{{transform:none}}")
BOB = "@keyframes bob{{" + "".join(bob) + "}}"
BOB = BOB.replace("{{", "{").replace("}}", "}")

css = f"""
text{{font-family:'Segoe UI',-apple-system,BlinkMacSystemFont,'Helvetica Neue',Arial,sans-serif}}
.mono{{font-family:ui-monospace,SFMono-Regular,Consolas,'Liberation Mono',Menlo,monospace}}
.panel{{fill:#0b1222;fill-opacity:.82;stroke:#3a5a9a;stroke-opacity:.7}}
.label{{fill:#8fa3bf}}
.val{{fill:#ffffff}}
.row{{fill:#ffffff;fill-opacity:.06;stroke:#ffffff;stroke-opacity:.18}}
.reticle{{fill:none;stroke:#ffffff;stroke-width:3;stroke-linecap:round}}
.tag{{fill:#0b1222;fill-opacity:.85}}
.step{{fill:#6f7f99}}
.ret,.ret-out,.glow,.ring,.feed,.card,.r1,.r2,.r3,.menu,.done{{transform-box:fill-box}}

.cycle{{animation:cycle {T}s infinite both}}
@keyframes cycle{{0%,{pct(FADE)}{{opacity:1}}{pct(FADE + 0.8)},100%{{opacity:0}}}}

.card{{animation:card {T}s infinite both}}
{appear_until("card", HEAR - 0.3, CLEAR)}
.r1{{animation:r1 {T}s infinite both}}  {appear("r1", HEAR)}
.r2{{animation:r2 {T}s infinite both}}  {appear("r2", SEE)}
.r3{{animation:r3 {T}s infinite both}}  {appear("r3", RECALL)}
.ret{{transform-origin:center;animation:ret {T}s infinite both}}
.ret-out{{transform-origin:center;animation:ret-out {T}s infinite both}}
{appear_until("ret-out", SEE, CLEAR, 0.5, "opacity:0;transform:scale(1.5)", "opacity:1;transform:scale(1)")}
{appear("ret", SEE, 0.5, "opacity:0;transform:scale(1.5)", "opacity:1;transform:scale(1)")}
.feed{{animation:feed {T}s infinite both}}
@keyframes feed{{0%,{pct(THINK - 0.6)}{{opacity:0;transform:translateX(0)}}{pct(THINK - 0.5)}{{opacity:1;transform:translateX(0)}}{pct(THINK)}{{opacity:1;transform:translateX(-58px)}}{pct(THINK + 0.1)},100%{{opacity:0;transform:translateX(-58px)}}}}

.glow{{transform-origin:center;animation:glow {T}s infinite both}}
@keyframes glow{{0%,{pct(THINK)}{{opacity:.35;transform:scale(.85)}}{pct(THINK + 0.5)}{{opacity:1;transform:scale(1.1)}}{pct(THINK + 1.0)}{{opacity:.7;transform:scale(.95)}}{pct(THINK + 1.5)}{{opacity:1;transform:scale(1.1)}}{pct(THINK + 2.0)}{{opacity:.7;transform:scale(.95)}}{pct(PICK)}{{opacity:1;transform:scale(1.05)}}{pct(PICK + 1.5)},100%{{opacity:.35;transform:scale(.85)}}}}
.ring{{transform-origin:center;animation:ring {T}s infinite both}}
@keyframes ring{{0%,{pct(THINK)}{{opacity:0;transform:scale(1)}}{pct(THINK + 0.1)}{{opacity:.8;transform:scale(1)}}{pct(THINK + 1.2)}{{opacity:0;transform:scale(1.8)}}{pct(THINK + 1.21)}{{opacity:.8;transform:scale(1)}}{pct(THINK + 2.3)},100%{{opacity:0;transform:scale(1.8)}}}}
.lobes{{animation:lobes {T}s infinite both}}
@keyframes lobes{{0%,{pct(THINK)}{{stroke:#7f9cc9}}{pct(THINK + 0.4)},{pct(PICK + 1.2)}{{stroke:#cfe0ff}}{pct(PICK + 1.8)},100%{{stroke:#7f9cc9}}}}

.menu{{animation:menu {T}s infinite both}} {appear_until("menu", MENU, CLEAR)}
.masked{{opacity:.4;animation:masked {T}s infinite both}}
@keyframes masked{{0%,{pct(MASK)}{{opacity:1}}{pct(MASK + 0.4)},100%{{opacity:.4}}}}
.strike{{animation:strike {T}s infinite both}} {appear("strike", MASK, 0.4, "opacity:0", "opacity:1")}
.pick{{animation:pick {T}s infinite both}}
@keyframes pick{{0%,{pct(PICK)}{{fill:#ffffff;fill-opacity:.06}}{pct(PICK + 0.3)},100%{{fill:#1f5bd8;fill-opacity:1}}}}
.path{{stroke-dasharray:120;animation:path {T}s infinite both}}
@keyframes path{{0%,{pct(PATH)}{{stroke-dashoffset:120;opacity:1}}{pct(PATH + 0.7)},{pct(CLEAR)}{{stroke-dashoffset:0;opacity:1}}{pct(CLEAR + 0.4)},100%{{stroke-dashoffset:0;opacity:0}}}}
.hit{{animation:hit {T}s infinite both}}
@keyframes hit{{0%,{pct(PATH + 0.6)}{{stroke:#ffffff}}{pct(PATH + 0.9)},100%{{stroke:#4d8eff}}}}
.done{{animation:done {T}s infinite both}} {appear("done", DONE)}

.walker{{transform-box:view-box;transform-origin:{FEET[0]}px {FEET[1]}px;animation:walk {T}s infinite both linear}}
@keyframes walk{{0%{{opacity:0;transform:none}}{pct(0.5)},{pct(WALK)}{{opacity:1;transform:none}}{pct(ARRIVE)},{pct(FADE)}{{opacity:1;transform:{ARRIVED}}}{pct(FADE + 0.6)},100%{{opacity:0;transform:{ARRIVED}}}}}
.bob{{transform-box:view-box;transform-origin:{FEET[0]}px {FEET[1]}px;animation:bob {T}s infinite both linear}}
{BOB}
.s1{{animation:s1 {T}s infinite both}} .s2{{animation:s2 {T}s infinite both}} .s3{{animation:s3 {T}s infinite both}} .s4{{animation:s4 {T}s infinite both}} .s5{{animation:s5 {T}s infinite both}}
"""
for i, (a, b) in enumerate([(HEAR, SEE), (SEE, RECALL), (RECALL, THINK), (THINK, PICK), (PICK, FADE)], 1):
    css += f"@keyframes s{i}{{0%,{pct(a)}{{fill:#6f7f99}}{pct(a + 0.3)},{pct(b)}{{fill:#ffffff}}{pct(b + 0.3)},100%{{fill:#6f7f99}}}}\n"
css += """
@media (prefers-reduced-motion: reduce){
  *{animation:none !important}
  .masked{opacity:.4}
  .pick{fill:#1f5bd8;fill-opacity:1}
  .hit{stroke:#4d8eff}
  .feed,.ring{opacity:0}
}
"""

# Left hemisphere of the brain glyph, drawn around (0, 0); the right one is its mirror.
half = ("M-3,-27 C-11,-34 -25,-31 -29,-22 C-38,-20 -41,-8 -36,-2 C-43,5 -38,19 -28,20 "
        "C-26,29 -13,32 -6,27 C-4,30 -3,30 -3,27 Z")
folds = "M-24,-12 C-17,-15 -12,-9 -15,-2 M-29,7 C-22,3 -13,7 -13,14 M-12,-24 C-9,-18 -13,-13 -8,-8"

svg = f"""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1600 533" width="1600" height="533" role="img" aria-labelledby="t d">
  <!-- Generated by hero.py from hero-bg.jpg. Edit those, not this file. -->
  <title id="t">Agenerela. The model decides. Validated code executes.</title>
  <desc id="d">A village guard at night, with a glowing brain above him standing for the language model. He hears the player say "Go to the tower.", sees the tower and the bridge as targets, and recalls that the night watch began. His brain thinks; a menu offers move_to, talk and follow_player, with attack struck out as not legal now. He picks move_to, a line runs to the tower, the game's MoveTo(tower) runs after a second check, and he walks onto the bridge toward the tower.</desc>
  <style>{css}</style>
  <defs>
    <radialGradient id="halo"><stop offset="0" stop-color="#4d8eff" stop-opacity=".75"/><stop offset=".55" stop-color="#1f5bd8" stop-opacity=".25"/><stop offset="1" stop-color="#1f5bd8" stop-opacity="0"/></radialGradient>
  </defs>
  <image width="1600" height="533" href="data:image/jpeg;base64,{bg}"/>

  <!-- The guard, with the model as his brain; he walks to the tower once the game runs MoveTo -->
  <g class="walker"><g class="bob">
  <image x="847" y="88" width="184" height="392" href="data:image/png;base64,{guard}"/>
  <line x1="917" y1="134" x2="930" y2="186" stroke="#7f9cc9" stroke-width="2" stroke-dasharray="3 6" stroke-linecap="round"/>
  <circle cx="915" cy="90" r="62" fill="url(#halo)" class="glow"/>
  <circle cx="915" cy="90" r="40" fill="none" stroke="#4d8eff" stroke-width="2" class="ring"/>
  <g transform="translate(915 88)">
    <g class="lobes" fill="#1f5bd8" fill-opacity=".45" stroke="#7f9cc9" stroke-width="2.6" stroke-linejoin="round" stroke-linecap="round">
      <path d="{half}"/><path d="{half}" transform="scale(-1 1)"/>
      <path d="{folds}" fill="none"/><path d="{folds}" fill="none" transform="scale(-1 1)"/>
    </g>
  </g>
  <rect x="893" y="128" width="44" height="22" rx="11" class="tag"/>
  <text x="915" y="144" text-anchor="middle" font-size="14" font-weight="700" letter-spacing="1" fill="#cfe0ff">LLM</text>
  </g></g>

  <g class="cycle">
    <!-- What it hears, sees and remembers -->
    <g class="card">
      <rect x="1016" y="22" width="276" height="122" rx="10" class="panel" stroke-width="1.5"/>
      <g class="r1"><text x="1034" y="56" font-size="15" class="label">hears</text><text x="1104" y="57" font-size="19" class="val">“Go to the tower.”</text></g>
      <g class="r2"><text x="1034" y="92" font-size="15" class="label">sees</text><text x="1104" y="93" font-size="19" class="val">tower · bridge</text></g>
      <g class="r3"><text x="1034" y="128" font-size="15" class="label">recalls</text><text x="1104" y="129" font-size="19" class="val">night watch began</text></g>
    </g>
    <circle cx="1012" cy="88" r="5" fill="#4d8eff" class="feed"/>

    <!-- Targets in the world -->
    <g class="ret">
      <path class="reticle hit" d="M1330,90 v-20 h20 M1478,70 h20 v20 M1498,242 v20 h-20 M1350,262 h-20 v-20"/>
      <rect x="1384" y="268" width="60" height="24" rx="6" class="tag"/>
      <text x="1414" y="285" text-anchor="middle" font-size="15" class="mono val">tower</text>
    </g>
    <g class="ret-out">
      <path class="reticle" d="M1066,384 v-12 h14 M1176,372 h14 v12 M1190,412 v12 h-14 M1080,424 h-14 v-12"/>
      <rect x="1096" y="430" width="64" height="24" rx="6" class="tag"/>
      <text x="1128" y="447" text-anchor="middle" font-size="15" class="mono val">bridge</text>
    </g>

    <!-- What it may do: the legal actions -->
    <g class="menu">
      <rect x="1016" y="168" width="222" height="186" rx="10" class="panel" stroke-width="1.5"/>
      <rect x="1028" y="180" width="198" height="36" rx="6" class="row pick"/>
      <text x="1042" y="205" font-size="18" class="mono val">move_to</text>
      <rect x="1028" y="222" width="198" height="36" rx="6" class="row"/>
      <text x="1042" y="247" font-size="18" class="mono val">talk</text>
      <rect x="1028" y="264" width="198" height="36" rx="6" class="row"/>
      <text x="1042" y="289" font-size="18" class="mono val">follow_player</text>
      <g class="masked">
        <rect x="1028" y="306" width="198" height="36" rx="6" fill="none" stroke="#ffffff" stroke-opacity=".35" stroke-dasharray="5 4"/>
        <text x="1042" y="331" font-size="18" class="mono val">attack</text>
      </g>
      <g class="strike">
        <line x1="1038" y1="325" x2="1112" y2="325" stroke="#f85149" stroke-width="2.5"/>
        <text x="1214" y="330" text-anchor="end" font-size="13" font-style="italic" fill="#ff7b72">not legal now</text>
      </g>
    </g>
    <path d="M1240,198 C1280,198 1290,170 1324,170" fill="none" stroke="#4d8eff" stroke-width="4" stroke-linecap="round" class="path"/>

    <!-- The game's own code runs it -->
    <g class="done">
      <rect x="1322" y="300" width="184" height="32" rx="8" class="tag" stroke="#3fb950" stroke-opacity=".6"/>
      <text x="1336" y="322" font-size="16" class="mono" fill="#56d364">✓ MoveTo(tower)</text>
    </g>
  </g>

  <!-- The beats, lit in turn -->
  <text x="82" y="446" font-size="24" font-weight="600"><tspan class="step s1">hears</tspan><tspan class="step" dx="10">·</tspan><tspan class="step s2" dx="10">sees</tspan><tspan class="step" dx="10">·</tspan><tspan class="step s3" dx="10">remembers</tspan><tspan class="step" dx="10">·</tspan><tspan class="step s4" dx="10">decides</tspan><tspan class="step" dx="10">·</tspan><tspan class="step s5" dx="10">acts</tspan></text>
</svg>
"""
open(os.path.join(HERE, "hero.svg"), "w", encoding="utf-8", newline="\n").write(svg)
print("hero.svg", len(svg) // 1024, "KB")
