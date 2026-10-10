// Builds Agenerela-Project-Design.pptx: the COMP 490 project design presentation.
//
// Needs Node with pptxgenjs, react, react-dom, react-icons and sharp installed somewhere on
// NODE_PATH (they are not project dependencies):
//     npm install pptxgenjs react react-dom react-icons sharp      (in any scratch folder)
//     NODE_PATH=<that folder>/node_modules node build-deck.js
//
// The figures come from diagrams/png/, rendered by diagrams/render.ps1. Fonts are Arial
// and Courier New only, because the team edits the deck in Google Slides.

const path = require("path");
const pptxgen = require("pptxgenjs");
const React = require("react");
const RDS = require("react-dom/server");
const sharp = require("sharp");
const fa = require("react-icons/fa6");

const HERE = __dirname;
const PNG = (n) => path.join(HERE, "diagrams", "png", n + ".png");
const OUT = process.env.DECK_OUT || path.join(HERE, "Agenerela-Project-Design.pptx");

// ---------- design tokens ----------
const C = {
  ink: "1C1C1C", ink2: "4A4A4A", ink3: "7F7F7F", line: "C2C2C2", rule: "D9D9D9",
  fill: "F1F3F7", fill2: "F7F8FA", blue: "1F5FD1", blueLt: "E8EFFC", white: "FFFFFF",
  dark: "1C1C1C", dark2: "26292F", onDark: "C9CCD3", onDark2: "8A8F98", blueOnDark: "8FB3F5",
};
const F = { sans: "Arial", mono: "Courier New" };
// One type scale for every slide (rubric: consistent font size).
const S = { title: 26, kicker: 10, head: 13, body: 11.5, small: 10.5, cap: 9, stat: 18 };

const TEAM = {
  yevhen: "Yevhen Mishchenko", hunter: "Hunter Hudson", tim: "Timothy Brustinov",
  tigran: "Tigran Kolsuzyan", hero: "Hero Jaiyen", maxim: "Maxim Goloubitsky",
};
const TOTAL = 20;

const pres = new pptxgen();
pres.layout = "LAYOUT_16x9"; // 10 x 5.625 in, the template's size
pres.title = "Project Design for Agenerela";
pres.author = "Agenerela team";
pres.company = "California State University, Northridge";

// ---------- helpers ----------
// "**bold**" and "`code`" inside a string become runs.
function runs(str, base) {
  const out = [];
  const re = /(\*\*[^*]+\*\*|`[^`]+`)/g;
  let last = 0, m;
  while ((m = re.exec(str))) {
    if (m.index > last) out.push({ text: str.slice(last, m.index), options: { ...base } });
    const t = m[0];
    if (t.startsWith("**")) out.push({ text: t.slice(2, -2), options: { ...base, bold: true } });
    else out.push({ text: t.slice(1, -1), options: { ...base, fontFace: F.mono } });
    last = m.index + t.length;
  }
  if (last < str.length) out.push({ text: str.slice(last), options: { ...base } });
  return out;
}

function text(s, str, o) {
  const base = { fontFace: F.sans, fontSize: o.size || S.body, color: o.color || C.ink };
  if (o.bold) base.bold = true;
  if (o.italic) base.italic = true;
  s.addText(runs(str, base), {
    x: o.x, y: o.y, w: o.w, h: o.h, margin: o.margin ?? 0, valign: o.valign || "top",
    align: o.align || "left", isTextBox: true, paraSpaceAfter: o.psa || 0,
    lineSpacingMultiple: o.lsm || 1.0, charSpacing: o.cs,
  });
}

function header(s, n, kicker, title, presenter) {
  s.background = { color: C.white };
  text(s, kicker, { x: 0.5, y: 0.26, w: 6.3, h: 0.22, size: S.kicker, bold: true, color: C.blue, cs: 1.5 });
  text(s, title, { x: 0.5, y: 0.5, w: 6.4, h: 0.5, size: S.title, bold: true });
  s.addText(
    [{ text: "Presenter: ", options: { color: C.ink3 } }, { text: presenter, options: { bold: true, color: C.ink } }],
    { shape: pres.shapes.ROUNDED_RECTANGLE, rectRadius: 0.15, x: 6.9, y: 0.27, w: 2.6, h: 0.3,
      fill: { color: C.fill }, line: { color: C.fill }, fontFace: F.sans, fontSize: 10, align: "center", valign: "middle", margin: 0 });
  text(s, `${n} / ${TOTAL}`, { x: 8.9, y: 5.28, w: 0.6, h: 0.2, size: S.cap, color: C.ink3, align: "right" });
}

function caption(s, str) {
  text(s, str, { x: 0.5, y: 5.28, w: 8.3, h: 0.2, size: S.cap, italic: true, color: C.ink3 });
}

// Status of the component on the slide: built (blue) or designed only (dashed).
// Status of what the slide shows, in one word: "built", "work" (In work) or "design".
function tag(s, status) {
  const [label, fill, color, line] = {
    built: ["Built", C.blue, C.white, { color: C.blue, width: 1 }],
    work: ["In work", C.white, C.blue, { color: C.blue, width: 1.25 }],
    design: ["Design", C.white, C.ink2, { color: C.ink3, width: 1, dashType: "dash" }],
  }[status];
  s.addText(label, {
    shape: pres.shapes.ROUNDED_RECTANGLE, rectRadius: 0.12, x: 8.5, y: 0.66, w: 1.0, h: 0.26,
    fill: { color: fill }, line, fontFace: F.sans, fontSize: 9, bold: true, color,
    align: "center", valign: "middle", margin: 0,
  });
}

function num(s, x, y, n, d = 0.3, fill = C.blue, color = C.white) {
  s.addText(String(n), {
    shape: pres.shapes.OVAL, x, y, w: d, h: d, fill: { color: fill }, line: { color: fill },
    fontFace: F.sans, fontSize: d >= 0.3 ? 12 : 10, bold: true, color, align: "center", valign: "middle", margin: 0,
  });
}

// Right-hand column of numbered points beside a diagram.
function points(s, items, x, y, w, step) {
  items.forEach(([head, body], i) => {
    const yy = y + i * step;
    num(s, x, yy, i + 1, 0.28);
    text(s, head, { x: x + 0.4, y: yy - 0.01, w: w - 0.4, h: 0.3, size: S.head, bold: true });
    text(s, body, { x: x + 0.4, y: yy + 0.3, w: w - 0.4, h: step - 0.38, size: S.body, color: C.ink2 });
  });
}

// Three takeaways under a full-width diagram.
function chips(s, items, y, h) {
  items.forEach(([lead, rest], i) => {
    const x = 0.5 + i * 3.05;
    s.addShape(pres.shapes.RECTANGLE, { x, y, w: 2.9, h, fill: { color: C.fill }, line: { color: C.fill } });
    s.addText([...runs(lead + " ", { fontFace: F.sans, fontSize: S.small, bold: true, color: C.ink }),
               ...runs(rest, { fontFace: F.sans, fontSize: S.small, color: C.ink2 })],
      { x: x + 0.1, y, w: 2.7, h, margin: 0, valign: "middle", isTextBox: true });
  });
}

function figure(s, name, x, y, w, ratio, alt) {
  const h = w / ratio;
  s.addImage({ path: PNG(name), x, y, w, h, altText: alt });
  return h;
}

async function icon(Comp, color = "#FFFFFF") {
  const svg = RDS.renderToStaticMarkup(React.createElement(Comp, { color, size: "256" }));
  const buf = await sharp(Buffer.from(svg)).png().toBuffer();
  return "image/png;base64," + buf.toString("base64");
}

async function iconDot(s, x, y, d, data) {
  s.addShape(pres.shapes.OVAL, { x, y, w: d, h: d, fill: { color: C.blue }, line: { color: C.blue } });
  const pad = d * 0.25;
  s.addImage({ data, x: x + pad, y: y + pad, w: d - 2 * pad, h: d - 2 * pad });
}

const notes = (s, lines) => s.addNotes(lines.join("\n"));

// ---------- slides ----------
async function build() {
  const ic = {
    branch: await icon(fa.FaCodeBranch), chat: await icon(fa.FaComments), flag: await icon(fa.FaFlag),
    target: await icon(fa.FaBullseye), shield: await icon(fa.FaShieldHalved), chip: await icon(fa.FaMicrochip),
    pen: await icon(fa.FaPenToSquare), cloud: await icon(fa.FaCloud), repeat: await icon(fa.FaRepeat),
    check: await icon(fa.FaCheck),
  };

  // 1 ── Title ───────────────────────────────────────────────────────────────
  {
    const s = pres.addSlide();
    s.background = { color: C.dark };
    text(s, "COMP 490 · SECTION 04 · PROJECT DESIGN", { x: 0.6, y: 0.5, w: 8.8, h: 0.24, size: S.kicker, bold: true, color: C.blueOnDark, cs: 2 });
    text(s, "Project Design for Agenerela", { x: 0.6, y: 0.8, w: 8.8, h: 0.75, size: 40, bold: true, color: C.white });
    text(s, "A Unity framework where a language model decides and validated code executes",
      { x: 0.6, y: 1.58, w: 8.8, h: 0.35, size: 16, color: C.onDark });

    // the three zones of the framework figure, in miniature
    const by = 2.3, bh = 0.85;
    s.addShape(pres.shapes.ROUNDED_RECTANGLE, { x: 0.6, y: by, w: 2.4, h: bh, rectRadius: 0.08, fill: { color: C.dark2 }, line: { color: "5A5F68", width: 1.25 } });
    text(s, "YOUR GAME", { x: 0.75, y: by + 0.17, w: 2.1, h: 0.25, size: 12, bold: true, color: C.white, cs: 2 });
    text(s, "asks the agent, then acts", { x: 0.75, y: by + 0.47, w: 2.1, h: 0.25, size: S.small, color: C.onDark });
    s.addShape(pres.shapes.LINE, { x: 3.05, y: by + bh / 2, w: 0.45, h: 0, line: { color: C.onDark, width: 1.5, endArrowType: "triangle" } });
    s.addShape(pres.shapes.ROUNDED_RECTANGLE, { x: 3.55, y: by, w: 2.9, h: bh, rectRadius: 0.08, fill: { color: C.blue }, line: { color: C.blue } });
    text(s, "AGENERELA", { x: 3.7, y: by + 0.17, w: 2.6, h: 0.25, size: 12, bold: true, color: C.white, cs: 2 });
    text(s, "legal options out, answers checked", { x: 3.7, y: by + 0.47, w: 2.65, h: 0.25, size: S.small, color: C.white });
    s.addShape(pres.shapes.LINE, { x: 6.5, y: by + 0.32, w: 0.45, h: 0, line: { color: C.onDark, width: 1.5, endArrowType: "triangle" } });
    s.addShape(pres.shapes.LINE, { x: 6.5, y: by + 0.55, w: 0.45, h: 0, line: { color: C.onDark, width: 1.5, beginArrowType: "triangle" } });
    s.addShape(pres.shapes.ROUNDED_RECTANGLE, { x: 7.0, y: by, w: 2.4, h: bh, rectRadius: 0.08, fill: { color: C.dark }, line: { color: C.onDark2, width: 1.25, dashType: "dash" } });
    text(s, "THE MODEL", { x: 7.15, y: by + 0.17, w: 2.1, h: 0.25, size: 12, bold: true, color: C.white, cs: 2 });
    text(s, "chooses, never touches the game", { x: 7.15, y: by + 0.47, w: 2.2, h: 0.25, size: S.small, color: C.onDark });

    text(s, `${TEAM.yevhen}  ·  ${TEAM.hunter}  ·  ${TEAM.tim}`, { x: 0.6, y: 3.55, w: 8.8, h: 0.28, size: 13, bold: true, color: C.white });
    text(s, `${TEAM.tigran}  ·  ${TEAM.hero}  ·  ${TEAM.maxim}`, { x: 0.6, y: 3.85, w: 8.8, h: 0.28, size: 13, bold: true, color: C.white });
    text(s, "Department of Computer Science", { x: 0.6, y: 4.3, w: 8.8, h: 0.25, size: 12, color: C.onDark });
    text(s, "California State University, Northridge", { x: 0.6, y: 4.56, w: 8.8, h: 0.25, size: 12, color: C.onDark });
    text(s, "October 15, 2026", { x: 0.6, y: 4.95, w: 8.8, h: 0.22, size: S.small, color: C.onDark2 });
    notes(s, [
      "SPEAKER: Yevhen Mishchenko · about 20 seconds",
      "",
      "- Hi, we're Agenerela: Yevhen, Hunter, Tim, Tigran, Hero and Maxim.",
      "- Agenerela is a Unity framework that lets a language model choose what a game agent does next, only from actions the developer registered.",
      "- Point at the three boxes: the game asks, our framework checks, the model only chooses.",
    ]);
  }

  // 2 ── Outline ─────────────────────────────────────────────────────────────
  {
    const s = pres.addSlide();
    header(s, 2, "OUTLINE", "Outline", TEAM.yevhen);
    const cards = [
      ["Motivation", "Why this matters, what already exists, and the problem we chose", "Yevhen"],
      ["Goals", "Eight goals, and how we reach each one", "Hunter"],
      ["Architecture", "One decision through the framework, in nine steps", "Hunter"],
      ["Design models", "A figure for each component", "All six of us, one each"],
      ["Methods and evaluation", "What our prototype measured, and how we will measure", "Hero · Maxim"],
      ["References, Q&A", "Our sources, then your questions", "Maxim · everyone"],
    ];
    cards.forEach(([h, d, who], i) => {
      const x = 0.5 + (i % 3) * 3.05, y = 1.25 + Math.floor(i / 3) * 1.92;
      s.addShape(pres.shapes.RECTANGLE, { x, y, w: 2.9, h: 1.75, fill: { color: C.fill }, line: { color: C.fill } });
      num(s, x + 0.2, y + 0.2, i + 1, 0.36);
      text(s, h, { x: x + 0.2, y: y + 0.7, w: 2.5, h: 0.3, size: 14, bold: true });
      text(s, d, { x: x + 0.2, y: y + 1.0, w: 2.5, h: 0.45, size: S.body, color: C.ink2 });
      text(s, who, { x: x + 0.2, y: y + 1.45, w: 2.5, h: 0.2, size: S.cap, bold: true, color: C.blue });
    });
    caption(s, "About 15 minutes: roughly 2.5 minutes per presenter.");
    notes(s, [
      "SPEAKER: Yevhen Mishchenko · about 20 seconds",
      "",
      "- Six parts: why we're building this, our goals, the architecture, then each of us shows the component we own, and we close with what we measured and how we'll measure.",
      "- (Point to the order and the names; don't read the cards.)",
    ]);
  }

  // 3 ── Motivation: importance ──────────────────────────────────────────────
  {
    const s = pres.addSlide();
    header(s, 3, "MOTIVATION · WHY IT MATTERS", "Why game agents need this", TEAM.yevhen);
    const cols = [
      [ic.branch, "Hand-built selection doesn't scale", "Developers pick an agent's next behaviour with hand-written condition trees. Every new situation means more branches."],
      [ic.chat, "Situations are open-ended", "A turn report, a game event or a player's line: no tree has a branch for each, yet the agent must still choose sensibly."],
      [ic.flag, "Not every agent is a character", "Factions, colonies and countries choose moves too, with no body and no voice. Character tools don't fit them."],
    ];
    for (let i = 0; i < 3; i++) {
      const [img, h, b] = cols[i];
      const x = 0.5 + i * 3.05;
      await iconDot(s, x, 1.2, 0.42, img);
      text(s, h, { x: x + 0.55, y: 1.24, w: 2.4, h: 0.4, size: S.head, bold: true, valign: "middle" });
      text(s, b, { x, y: 1.72, w: 2.85, h: 0.85, size: S.body, color: C.ink2 });
    }
    figure(s, "02-agents", 0.5, 2.8, 9.0, 4.0, "A guard you talk to and a country with no body, both driven through Agenerela with the same API and the same checks.");
    caption(s, "Figure 1. One framework, two very different agents: our first two demo games. Scenes and moves are illustrative [2].");
    notes(s, [
      "SPEAKER: Yevhen Mishchenko · about 35 seconds",
      "",
      "- Today a developer picks an agent's next behaviour with hand-written condition trees, and every new situation means more branches.",
      "- And situations are open-ended: a turn report, a game event, sometimes a player's line. Usually the game itself decides it's time to ask.",
      "- Left, our village guard: 'Attack Godzilla' should be refused, because there is no Godzilla. Right, a country: no body, no voice.",
      "- One framework drives both, with the same API and the same checks.",
      "",
      "If asked 'Do you replace behaviour trees?': No. A tree ticks in microseconds for free. We replace only the top-level selection node, where hand-written trees grow combinatorially, and a tree can call our agent.",
    ]);
  }

  // 4 ── Motivation: existing work ───────────────────────────────────────────
  {
    const s = pres.addSlide();
    header(s, 4, "MOTIVATION · EXISTING WORK", "Existing work, and where it stops", TEAM.yevhen);
    const cards = [
      ["HAND-AUTHORED AI", "Behaviour trees, state machines", "Deterministic and nearly free: a tree ticks in microseconds.", "Every branch is written by hand; open-ended input is out of reach.", "Kelley [3] uses trees as scaffolding around LM agents: ours can sit inside one."],
      ["CHARACTER PLATFORMS", "Convai [5], Inworld AI [6]", "Conversational characters with voice, perception and animation.", "Built around characters that talk, not factions or colonies with no body.", ""],
      ["LLM INFERENCE IN UNITY", "LLMUnity [4]", "Runs llama.cpp inside a Unity game, with grammar-constrained output.", "Plumbing only: its function-calling sample is a fixed grammar over three zero-argument methods. No game state, no validation.", "We use it as a dependency."],
      ["DOING IT BY HAND", "Prompting a model yourself", "Understands almost any request.", "Free text to parse, no guarantees: in our prototype a 2B model was right about 60% of the time [1].", ""],
    ];
    cards.forEach(([cat, name, good, stop, foot], i) => {
      const x = 0.5 + i * 2.29, y = 1.2, w = 2.14, h = 3.55;
      s.addShape(pres.shapes.RECTANGLE, { x, y, w, h, fill: { color: C.fill }, line: { color: C.fill } });
      text(s, cat, { x: x + 0.15, y: y + 0.15, w: w - 0.3, h: 0.2, size: 8.5, bold: true, color: C.ink3, cs: 1 });
      text(s, name, { x: x + 0.15, y: y + 0.38, w: w - 0.3, h: 0.5, size: S.head, bold: true });
      text(s, "GOOD AT", { x: x + 0.15, y: y + 0.95, w: w - 0.3, h: 0.2, size: 8.5, bold: true, color: C.blue, cs: 1 });
      text(s, good, { x: x + 0.15, y: y + 1.15, w: w - 0.3, h: 0.7, size: S.small, color: C.ink2 });
      text(s, "WHERE IT STOPS", { x: x + 0.15, y: y + 1.85, w: w - 0.3, h: 0.2, size: 8.5, bold: true, color: C.ink, cs: 1 });
      text(s, stop, { x: x + 0.15, y: y + 2.05, w: w - 0.3, h: 1.0, size: S.small, color: C.ink });
      if (foot) text(s, foot, { x: x + 0.15, y: y + 3.0, w: w - 0.3, h: 0.45, size: S.cap, italic: true, color: C.ink3 });
    });
    text(s, "**The gap for developers:** nothing lets them plug a small, local model into their own actions safely, for any kind of agent.",
      { x: 0.5, y: 4.85, w: 9.0, h: 0.3, size: S.body, color: C.ink });
    caption(s, "Sources: [1] build plan §2.4, §5 and Appendix A; [3]–[6] in References.");
    notes(s, [
      "SPEAKER: Yevhen Mishchenko · about 45 seconds",
      "",
      "- Four kinds of existing work.",
      "- Behaviour trees are fast and free, but every branch is hand-written. Kelley's paper uses trees as scaffolding around language models; a tree can hand its open-ended decision to our agent.",
      "- Convai and Inworld build characters that talk. A country in a strategy game doesn't talk.",
      "- LLMUnity runs a model inside Unity. That's plumbing we'll use, but it doesn't decide what's legal or check the answer.",
      "- Prompting a model yourself gives free text and no guarantees: about 60% right in our prototype.",
      "- The gap is the layer in between, and that's our project.",
      "",
      "If asked 'Why not just use a bigger model?': On our prototype's 20 player commands, a 2B model with our checks beat a 4B without them, 95% to 65%. On our harder 130-prompt probe the checks helped both, but the 4B alone (113) still beat the 2B with the check (108). So the checks matter, and so does size; the 2B is what fits beside a game on an 8 GB card.",
    ]);
  }

  // 5 ── Motivation: the problem, for Unity developers ───────────────────────
  {
    const s = pres.addSlide();
    header(s, 5, "MOTIVATION · THE PROBLEM WE SOLVE", "The problem, for Unity developers", TEAM.yevhen);
    s.addShape(pres.shapes.RECTANGLE, { x: 0.5, y: 1.15, w: 5.3, h: 0.95, fill: { color: C.blueLt }, line: { color: C.blueLt } });
    text(s, "A Unity developer who wants agents to react to open-ended input has **no safe, reusable way** to let a language model decide what those agents do.",
      { x: 0.7, y: 1.2, w: 4.95, h: 0.85, size: 14, valign: "middle" });
    text(s, "WITHOUT A FRAMEWORK, EVERY DEVELOPER HAS TO", { x: 0.5, y: 2.3, w: 5.3, h: 0.22, size: 8.5, bold: true, color: C.ink3, cs: 1.5 });
    const pains = [
      [ic.pen, "Write prompts and parse replies", "for every agent, then turn free text back into game code."],
      [ic.shield, "Trust whatever comes back", "an action the agent can't do now, or a target that isn't there."],
      [ic.cloud, "Pay per request, or ship a server", "≈ $165 per player per 100 h at 30 agents; players won't run Ollama."],
      [ic.repeat, "Start over for each kind of agent", "a guard, a companion and a faction, each wired differently."],
    ];
    for (let i = 0; i < 4; i++) {
      const [img, h, b] = pains[i];
      const y = 2.62 + i * 0.62;
      await iconDot(s, 0.5, y, 0.4, img);
      text(s, h, { x: 1.05, y: y - 0.03, w: 4.75, h: 0.26, size: S.head, bold: true });
      text(s, b, { x: 1.05, y: y + 0.23, w: 4.75, h: 0.3, size: S.small, color: C.ink2 });
    }
    const px = 6.1, pw = 3.4;
    s.addShape(pres.shapes.RECTANGLE, { x: px, y: 1.15, w: pw, h: 3.95, fill: { color: C.dark }, line: { color: C.dark } });
    text(s, "WITH AGENERELA", { x: px + 0.25, y: 1.35, w: pw - 0.5, h: 0.22, size: 8.5, bold: true, color: C.blueOnDark, cs: 1.5 });
    const gains = [
      "Register actions, not prompts",
      "Only legal, grounded answers reach your code",
      "Runs locally, even inside the shipped game",
      "One API for any agent, with or without a body",
    ];
    for (let i = 0; i < 4; i++) {
      const y = 1.78 + i * 0.5;
      await iconDot(s, px + 0.25, y, 0.26, ic.check);
      text(s, gains[i], { x: px + 0.65, y: y - 0.04, w: pw - 0.9, h: 0.46, size: S.body, color: C.white });
    }
    text(s, "60% → 95%", { x: px + 0.25, y: 3.88, w: pw - 0.5, h: 0.5, size: 28, bold: true, color: C.white });
    text(s, "correct decisions on player commands, small local model, before and after our checks, in our prototype",
      { x: px + 0.25, y: 4.4, w: pw - 0.5, h: 0.5, size: S.small, color: C.onDark });
    caption(s, "Cost at Flash-Lite pricing, 6 decisions a minute [1, §5]; 60% → 95% on 20 player commands, qwen3.5 2B via Ollama [1, App. A].");
    notes(s, [
      "SPEAKER: Yevhen Mishchenko · about 40 seconds",
      "",
      "- This is the problem from a Unity developer's side: no safe, reusable way to let a language model decide what their agents do. (Read the blue box once.)",
      "- Without a framework they write prompts and parse replies, trust whatever comes back, pay per request or ship a server, and start over for each kind of agent.",
      "- With Agenerela they register actions, only legal answers reach their code, it runs locally, and one API covers any agent.",
      "- And in our prototype, checks like ours took a small model from 60 to 95 percent.",
      "",
      "HAND-OFF: \"Hunter will walk you through our goals and the architecture.\"",
      "",
      "If asked about sample size: 20 focused prompts; Phase 4 re-measures on 200+.",
      "If asked 'Isn't 60 to 95 the word check you reject on slide 16?': Yes. The prototype's check matched the player's words. On our harder 130-prompt probe it refused good requests, so we replaced it with a confidence check that scored higher. Hero shows both.",
    ]);
  }

  // 6 ── Goals ──────────────────────────────────────────────────────────────
  {
    const s = pres.addSlide();
    header(s, 6, "GOALS", "Goals, and how we reach each one", TEAM.hunter);
    const goals = [
      ["Any game entity can be an agent", "`Agent` is plain C#, so a country with no body uses the same API as a guard."],
      ["Developers define the actions", "Actions are data, on a C# method or an asset, run by the game's own handler."],
      ["Only legal actions, no made-up targets", "Each request lists only what is legal now; guards turn a bad choice into `none`."],
      ["Agents remember what happened", "The last few turns go into each request, recorded after the guards. Phase 2."],
      ["Any model, even inside the game", "One `ILLMProvider`: Ollama to build, llama.cpp to ship, cloud optional."],
      ["The game stays responsive", "Async requests; a queue serves player-facing requests first, with budgets."],
      ["Every accuracy claim is measured", "200+ labelled prompts, A/B runs with a control arm, five outcome classes."],
      ["A working agent in under 15 minutes", "Editor tools, three small samples, demos that use only the public API."],
    ];
    const w = 2.1375, ch = 1.6;
    goals.forEach(([h, b], i) => {
      const x = 0.5 + (i % 4) * (w + 0.15), y = 1.15 + Math.floor(i / 4) * (ch + 0.1);
      const mem = i === 3;
      s.addShape(pres.shapes.RECTANGLE, { x, y, w, h: ch, fill: { color: mem ? C.blueLt : C.fill }, line: { color: mem ? C.blueLt : C.fill } });
      num(s, x + 0.15, y + 0.13, i + 1, 0.28);
      text(s, h, { x: x + 0.15, y: y + 0.47, w: w - 0.3, h: 0.42, size: 12, bold: true });
      text(s, b, { x: x + 0.15, y: y + 0.9, w: w - 0.3, h: 0.66, size: S.small, color: C.ink2 });
    });
    const by = 1.15 + 2 * (ch + 0.1);
    s.addShape(pres.shapes.RECTANGLE, { x: 0.5, y: by, w: 9.0, h: 0.55, fill: { color: C.blue }, line: { color: C.blue } });
    text(s, "BY DECEMBER", { x: 0.7, y: by, w: 1.4, h: 0.55, size: 8.5, bold: true, color: C.white, cs: 1.5, valign: "middle" });
    text(s, "**Phases 0–4:** a measured framework, about 95% correct with guards, on two genres, one of them a faction with no body.",
      { x: 2.0, y: by, w: 7.35, h: 0.55, size: S.small, color: C.white, valign: "middle" });
    caption(s, "Goals from our system-design practice (24 Sep 2026); memory from build plan §2.9; semester target from §6.5 [1].");
    notes(s, [
      "SPEAKER: Hunter Hudson · about 55 seconds",
      "",
      "- Eight goals; each card says what, then how.",
      "- One: anything can be an agent, a guard or a country, because Agent is plain C#. Two: developers define their own actions, as data. Three: the model only sees what's legal now, and guards check every answer.",
      "- Four, highlighted: agents remember. Each request carries the last few turns, recorded after the guards, so the guard can follow 'Follow me' with 'Now wait here'. That's Phase 2.",
      "- Five: any model backend, even one inside the shipped game. Six: the game never freezes while an agent thinks. Seven: every accuracy claim comes from an A/B run. Eight: a working agent in under 15 minutes.",
      "- The blue bar is what we commit to by December.",
      "",
      "HAND-OFF (to yourself): \"Here is how those pieces fit together.\"",
    ]);
  }

  // 7 ── Architecture: the black box ────────────────────────────────────────
  {
    const s = pres.addSlide();
    header(s, 7, "ARCHITECTURE", "Architecture: the black box", TEAM.hunter);
    tag(s, "work");
    figure(s, "01-context", 0.8, 1.12, 8.4, 2.5, "Agenerela as a black box between the developer's game and the model backends, with the player and the game developer.");
    chips(s, [
      ["One package in the middle.", "Only Agenerela talks to the model, which never touches the game."],
      ["Blue is one decision.", "The game asks, the model answers three fields, the game acts."],
      ["Dashed means outside our control.", "Any backend can be swapped behind one interface."],
    ], 4.56, 0.6);
    caption(s, "Figure 2. Who and what Agenerela talks to. Our figure, redrawn from our requirements document [2].");
    notes(s, [
      "SPEAKER: Hunter Hudson · about 45 seconds",
      "",
      "- First from the outside. The black box is our package.",
      "- Left, the developer's game: it decides when to ask an agent and runs the chosen action in its own code. The player only sees the game.",
      "- Right, the model backends: Ollama, a model inside the game, or a cloud API. Dashed, because they're outside our control and we don't trust them.",
      "- Blue arrows: the game asks with a stimulus and observations, we send a prompt and a schema, the model answers with three fields, and the game gets a decision plus telemetry.",
      "- At the top, the developer configures it and reads our reports.",
      "",
      "HAND-OFF (to yourself): \"Now let's open the black box.\"",
    ]);
  }

  // 8 ── Inside the box: one decision (Hunter) ───────────────────────────────
  {
    const s = pres.addSlide();
    header(s, 8, "DESIGN MODELS · AGENT AND DECISION REQUESTS", "Inside the box: one decision", TEAM.hunter);
    tag(s, "work");
    const fh = 3.95, fw = fh * 1.2075;
    s.addImage({ path: PNG("00-architecture"), x: 0.5, y: 1.15, w: fw, h: fh,
      altText: "Agenerela framework design: your game, the Agenerela package and the model, with one decision passing through nine numbered steps." });
    const x = 0.5 + fw + 0.3, w = 9.5 - x;
    text(s, "Blue arrows: one decision, steps 1–9 · dashed: reads state · ○ an interface you implement",
      { x, y: 1.15, w, h: 0.38, size: S.cap, color: C.ink3 });
    points(s, [
      ["The game asks", "`DecideAsync` with an event, a report or a player's line, whenever it decides."],
      ["The agent remembers", "Step 2: who it is, what it sees, and its last few turns (Phase 2)."],
      ["Only legal options go out", "Actions legal right now and targets in reach become the schema."],
      ["Checked before anything runs", "Three fields come back; guards check them, `Execute` re-checks."],
    ], x, 1.68, w, 0.86);
    caption(s, "Figure 3. Agenerela framework design: one decision in nine steps. Our figure, docs/design [2].");
    notes(s, [
      "SPEAKER: Hunter Hudson · about 60 seconds",
      "",
      "- Same three columns, opened up. This is my component: the agent and the decision request.",
      "- One: the game calls DecideAsync, usually with an event or a report, sometimes a player's line. Two: the agent gathers who it is, what it sees, and, from Phase 2, what it remembers.",
      "- Three and four: only the actions legal now and the targets in reach go into the schema and prompt. Five: the request is queued, player-facing first, and sent to a provider. Six: the model picks.",
      "- Seven: guards check the answer. Eight: the result and telemetry come back. Nine: the game's own code acts, and Execute re-checks first, because the world may have changed.",
      "- The types and registries are merged; the Agent class itself is next (#17).",
      "",
      "HAND-OFF: \"Tim will show how a developer defines the actions in step 3.\"",
      "",
      "If asked 'Why split DecideAsync and Execute?': so the game can act at the right moment, after an animation say, and execution is validated independently of the model.",
      "If asked 'What if the provider fails?': it comes back as a result with outcome PipelineError, never an exception.",
    ]);
  }

  // 9 ── Action vocabulary (Tim) ─────────────────────────────────────────────
  {
    const s = pres.addSlide();
    header(s, 9, "DESIGN MODELS · ACTION VOCABULARY", "Two doors, one registry", TEAM.tim);
    tag(s, "built");
    figure(s, "03-actions", 0.7, 1.12, 8.6, 2.6087, "Two ways to define an action, a tagged C# method or an asset, both producing one ActionDefinition that the registry binds to the game's handler.");
    chips(s, [
      ["Actions are data, not an enum.", "Each game brings its own verbs; the framework ships none."],
      ["Two doors, one result.", "A tagged method or an asset; nothing downstream can tell which."],
      ["The model never runs code.", "It picks an id; the registry finds your handler."],
    ], 4.5, 0.64);
    caption(s, "Figure 4. How an action is defined and bound to game code, as merged (#4, #6, #15, #50). Our figure [1, §2.2, DR-011].");
    notes(s, [
      "SPEAKER: Timothy Brustinov · about 60 seconds",
      "",
      "- This part is built and merged.",
      "- Actions are data the developer authors, not a list in our code. Two ways in: tag a method in your own script, top left, or fill in an asset in the Inspector, bottom left, with no code.",
      "- Both produce the same ActionDefinition: an id the model can pick, a short description, whether it needs a target, and an example for the prompt.",
      "- The ActionRegistry binds each id to one piece of game code. The model only picks an id; your handler says whether it's legal now and what happens. 'none' is always added last.",
      "- If both doors define one id, the asset's wording wins and the method still runs it, so we can A/B-test wording without recompiling.",
      "",
      "HAND-OFF (to yourself): \"Here's where a developer or writer edits all of this.\"",
      "",
      "If asked 'Why not an enum?': the prototype's fixed enum is exactly what this rewrite exists to remove; every game needs its own verbs.",
    ]);
  }

  // 10 ── Agent Profile screen (Tim) ─────────────────────────────────────────
  {
    const s = pres.addSlide();
    header(s, 10, "DESIGN MODELS · UI DESIGN", "Where an agent is authored", TEAM.tim);
    tag(s, "design");
    const fh = 3.95, fw = fh * 1.0395;
    s.addImage({ path: PNG("wf-profile"), x: 0.5, y: 1.15, w: fw, h: fh,
      altText: "Wireframe of the Agent Profile Inspector: identity fields, the action list with a length check, an expanded action, the locked none row and a problem bar." });
    s.addShape(pres.shapes.RECTANGLE, { x: 0.5, y: 1.15, w: fw, h: fh, fill: { type: "none" }, line: { color: C.line, width: 0.75 } });
    const x = 0.5 + fw + 0.3, w = 9.5 - x;
    const items = [
      ["Identity", "Name, role, personality, goals. The prompt is built from these."],
      ["Action list", "All it can ever do; the game decides what is legal now."],
      ["Length check", "Descriptions of similar length: a long one biases small models."],
      ["Edit in place", "Example wording and sensible targets, with a preview."],
      ["Built-in `none`", "Added by the framework, locked, always last."],
      ["Problems where you look", "An empty name, an empty row or a duplicate id, with a fix."],
    ];
    items.forEach(([h, b], i) => {
      const y = 1.2 + i * 0.64;
      num(s, x, y + 0.02, i + 1, 0.26);
      text(s, h, { x: x + 0.38, y, w: w - 0.38, h: 0.26, size: S.head, bold: true });
      text(s, b, { x: x + 0.38, y: y + 0.26, w: w - 0.38, h: 0.34, size: S.small, color: C.ink2 });
    });
    caption(s, "Figure 5. Agent Profile & Actions screen, a wireframe from our sketch lab (17 Sep 2026); sample data [2].");
    notes(s, [
      "SPEAKER: Timothy Brustinov · about 60 seconds",
      "",
      "- This is our screen design for authoring an agent, from our sketch lab. The numbers on the screen match the list.",
      "- One: a writer fills in name, role, personality and goals once, and we build the prompt from them. Two: the action list, everything this agent can ever do; the game decides what's legal at the moment.",
      "- Three: a length check, because in our prototype one long description pulled a small model toward that action. Four: expand a row to edit its example and the targets it makes sense for.",
      "- Five: 'none' is added by us, locked and always last. Six: problems show up right here, not just in the Console.",
      "- The profile asset and its checks are merged; this Inspector is Phase 5 work.",
      "",
      "HAND-OFF: \"Tigran will show how the agent's state decides which of these actions the model actually sees.\"",
    ]);
  }

  // 11 ── Availability (Tigran) ──────────────────────────────────────────────
  {
    const s = pres.addSlide();
    header(s, 11, "DESIGN MODELS · ACTION AVAILABILITY", "What the model is offered", TEAM.tigran);
    tag(s, "built");
    figure(s, "04-availability", 0.7, 1.12, 8.6, 2.6087, "The village guard at its post, walking and following, with the actions offered in each, and how the list is built on every decision.");
    chips(s, [
      ["Legal now, or absent.", "Each handler's `IsAvailable` runs on every decision."],
      ["Nothing to name?", "Actions that need a target are skipped too."],
      ["`none` is always last.", "So it reads as a fallback, not as the default."],
    ], 4.5, 0.64);
    caption(s, "Figure 6. Action availability. The guard's situations are illustrative; the filtering rule is the merged code (#7) [1, §2.2].");
    notes(s, [
      "SPEAKER: Tigran Kolsuzyan · about 60 seconds",
      "",
      "- Top: the same guard in three situations. Blue chips are offered to the model; gray, crossed-out ones are absent.",
      "- While it's walking, move_to is absent, because its CanMove check is false. While it's following you, follow_player is absent.",
      "- That's state masking: we never tell the model 'don't do X'. X just isn't on the list, so the model can't say it.",
      "- Bottom, and this is merged code: for every action, skip it if it needs a target and nothing is in reach, skip it if its handler says no, offer the rest, then 'none' last.",
      "- 'none' goes last because in our prototype an emphasized 'none' pulled a small model toward doing nothing. The list is rebuilt on every decision.",
      "",
      "HAND-OFF (to yourself): \"Here's exactly what the model receives while the guard is following.\"",
    ]);
  }

  // 12 ── Schema and prompt (Tigran) ─────────────────────────────────────────
  {
    const s = pres.addSlide();
    header(s, 12, "DESIGN MODELS · DECISION SCHEMA", "What the model receives", TEAM.tigran);
    tag(s, "work");
    figure(s, "05-schema", 0.7, 1.12, 8.6, 2.6087, "The five prompt blocks in order, and the JSON schema built for the guard while following, with the model's three-field answer.");
    chips(s, [
      ["Built fresh for every request.", "From the legal actions, targets in reach and recent turns."],
      ["Field order is measured.", "action → target → statement: +11.7 points in the prototype."],
      ["One schema, two formats.", "JSON Schema for Ollama [7] and cloud [10]; GBNF for llama.cpp [8]."],
    ], 4.5, 0.64);
    caption(s, "Figure 7. The prompt and schema for one decision. Example wording is illustrative. Our design [1, §2.3, §2.9].");
    notes(s, [
      "SPEAKER: Tigran Kolsuzyan · about 60 seconds",
      "",
      "- Left: the prompt, in reading order. The system prompt from the profile; examples, one per legal action plus an idle one and a refusal; observations, what's true now; history, the agent's memory, from Phase 2; and the stimulus last.",
      "- The example builder is merged. Its refusal example puts a made-up name into a real example, so the model sees how to refuse something that isn't there.",
      "- Right: the schema for this one request. Only legal actions, 'none' last; target required, with no_target; and the order action, target, then statement.",
      "- Order matters: writing the chat text first made the model talk itself into 'no action'. Action first was worth 11.7 points.",
      "- The same schema becomes JSON Schema for Ollama and cloud APIs, or a grammar for llama.cpp, so the model can't write an action that isn't listed.",
      "",
      "HAND-OFF: \"Yevhen will show where that model actually runs.\"",
      "",
      "If asked 'Why no reasoning field?': we tried one before the action, and it scored worse than no change at all, 35.3 against 41.2 percent.",
    ]);
  }

  // 13 ── Providers (Yevhen) ─────────────────────────────────────────────────
  {
    const s = pres.addSlide();
    header(s, 13, "DESIGN MODELS · MODEL PROVIDERS", "Where the model runs", TEAM.yevhen);
    tag(s, "design");
    figure(s, "06-providers", 0.7, 1.12, 8.6, 2.6087, "Agenerela plugs into one ILLMProvider interface with three providers: Ollama to build, llama.cpp inside the game to ship, and a cloud API to compare.");
    chips(s, [
      ["Three providers, three jobs.", "Ollama to build, in-process to ship, cloud to compare."],
      ["The provider is untrusted.", "Guards check its output whatever it claims."],
      ["Keys never ship.", "Read from `.env`, sent in a header, redacted from errors."],
    ], 4.5, 0.64);
    caption(s, "Figure 8. Where the model runs. Our design [1, §2.4, DR-001]; LLMUnity [4], Qwen [9], Gemini [10].");
    notes(s, [
      "SPEAKER: Yevhen Mishchenko · about 45 seconds",
      "",
      "- All three options plug into one interface, ILLMProvider, which is merged. Solid boxes are our classes; dashed boxes are what they call.",
      "- Build: the Editor talks to a local Ollama server, so swapping models takes seconds. A 2B model plus a scene used 4.2 of 8 GB on our laptop.",
      "- Ship: players won't install a server, so the model runs inside the game through llama.cpp, via LLMUnity. About 1 GB more, and no network.",
      "- Compare: cloud APIs, Gemini first. Cost grows with every agent, and keys never ship.",
      "",
      "HAND-OFF: \"Hero will show how targets are found and how the guards check the model's answer.\"",
      "",
      "If asked 'Why not write the llama.cpp integration yourselves?': native binaries for every platform is a semester of work and not our contribution (DR-001).",
    ]);
  }

  // 14 ── Targets (Hero) ───────────────────────────────────────────────────
  {
    const s = pres.addSlide();
    header(s, 14, "DESIGN MODELS · TARGETS", "Targets: what an agent may name", TEAM.hero);
    tag(s, "built");
    figure(s, "07-targets", 0.7, 1.12, 8.6, 2.6087, "The Targetable component on a scene object, the area ProximityTargetSource searches around the agent, and the resulting list of names.");
    chips(s, [
      ["Targetable is the tag.", "Its id is the exact word the model may answer with."],
      ["The area is one option.", "A ready-made query for scene agents, used only if you add it."],
      ["Or your own rule.", "Your own target source decides; a country lists its targets."],
    ], 4.5, 0.64);
    caption(s, "Figure 9. The tag, one way to choose targets, and the result, as merged (#32). Scene layout is illustrative [1, §2.2, DR-014].");
    notes(s, [
      "SPEAKER: Hero Jaiyen · about 55 seconds",
      "",
      "- Before the model picks a target, we decide what it may name. This part is merged.",
      "- Left: Targetable is the tag. Its Id is the exact word the model may answer with, set by hand. Its description is the line the agent reads about it.",
      "- Middle: one way to choose, the area. It finds tagged objects within 18 metres, nearest first, at most 8. Houses have no tag, so they're scenery.",
      "- That area is only one option, used if the developer adds it. Their own target source can follow what a faction scouted or a quest stage; a country just lists its targets.",
      "- Right: whichever way it's built, the list plus no_target becomes the schema's target list.",
      "",
      "HAND-OFF (to yourself): \"Now the question everyone asks: what stops 'Attack Godzilla'?\"",
    ]);
  }

  // 15 ── Guardrails for Attack Godzilla (Hero) ─────────────────────────────
  {
    const s = pres.addSlide();
    header(s, 15, "DESIGN MODELS · GUARDS", "Guardrails for \"Attack Godzilla\"", TEAM.hero);
    tag(s, "design");
    figure(s, "07-guards", 0.7, 1.12, 8.6, 2.6087, "Three guardrails against 'Attack Godzilla': the target list and a refusal example in the prompt before the model answers, and checks in code after it on every decision, among them a confidence check that turns a target the model reached for off the list into none. Below, what each measured.");
    chips(s, [
      ["Two guardrails before the model.", "The list limits what it can say; the prompt shows how to refuse."],
      ["One after it, on every decision.", "If under 80% of its choice stayed on the list, nothing runs."],
      ["Measured on 130 prompts.", "2B: 83 → 108 correct. A name check got 97 and refused 29 good requests."],
    ], 4.5, 0.64);
    caption(s, "Figure 10. Three guardrails, checked on every decision; results from our probe, Oct 2026 [1, §2.5, DR-016].");
    notes(s, [
      "SPEAKER: Hero Jaiyen · about 65 seconds",
      "",
      "- There's no Godzilla, so the guard should refuse. Three guardrails stop it, on every call, whoever made it.",
      "- One, the target list: 'godzilla' isn't an id, so the model can't write it. But a small model often grabs the closest legal target and attacks the training dummy, which looks like obedience.",
      "- Two, the prompt: a refusal example and no_target show it how to say 'that isn't here'. That stops most requests for things that aren't there at all.",
      "- Three, after the model: the legality check, Execute's re-check, and a confidence check. The model's own odds show what it wanted. A real request keeps 99.8% on the list; 'Attack the scarecrow' kept 5%. Below 80%, nothing runs.",
      "- On 130 prompts, the 2B went from 83 to 108 correct. Checking the player's words got 97 and refused 29 good requests.",
      "- Honest part: the 80% line comes from our own prompts; Phase 4 recalibrates it.",
      "",
      "HAND-OFF (to yourself): \"Here's everything we measured.\"",
      "",
      "If asked 'Why not just tell the model to refuse?': we do, with the refusal example, and a one-line rule helps too (73 to 88 on the 2B). But 'scarecrow' can't be written, so its probability moves onto a legal id, and small models still guess.",
      "If asked 'What about \"attack the training thing\"?': the model maps it to the training dummy, and if it's sure, it acts. No word lists to maintain.",
      "If asked 'Does it stop the agent choosing freely?': no. A model torn between two good targets keeps its odds on the list; our 4B test passed every free choice.",
      "If asked 'What still gets through?': a near miss the model is sure of, like a bucket taken for the barrel on the 4B.",
      "If asked 'Is it built?': the guard is Phase 3. The interface that carries the model's odds is merged (#68); no provider fills it yet.",
    ]);
  }

  // 15 ── Methodologies (Hero) ───────────────────────────────────────────────
  {
    const s = pres.addSlide();
    header(s, 16, "METHODOLOGIES", "Methods, and what each measured", TEAM.hero);
    const hdr = (t) => ({ text: t, options: { bold: true, fontSize: 8.5, color: C.ink3, charSpacing: 1 } });
    const bottom = { type: "solid", pt: 0.75, color: C.rule };
    const none = { type: "none" };
    const cell = (t, o = {}) => ({ text: t, options: { border: [none, none, bottom, none], ...o } });
    const rows = [
      ["Per-request schema + few-shot, vs. prose rules", "58.5% → 84.9%", "correct, A/B with 53 prompts per arm; wrong-but-legal 20.8% → 3.8%", "ADOPT"],
      ["Generated few-shot examples", "+35.3 pts", "largest single lever (17 prompts)", "ADOPT"],
      ["Action before the free-text field", "+11.7 pts", "isolated, five-variant head-to-head", "ADOPT"],
      ["target required, with a no_target value", "0/5 → 5/5", "4B model naming the target", "ADOPT"],
      ["Confidence check: what stayed on the list", "83 → 108", "of 130, 2B; refused 17 good requests (14 without it); 4B 113 → 124", "ADOPT"],
      ["Name check on the player's words", "83 → 97", "of 130, 2B; refused 29 good requests; 4B 113 → 109", "REJECTED"],
      ["Reasoning field before action", "35.3% vs 41.2%", "worse than no change at all", "REJECTED"],
    ];
    const table = [[hdr("METHOD"), hdr("MEASURED"), hdr("CONDITIONS"), hdr("VERDICT")].map((c) => ({ ...c, options: { ...c.options, border: [none, none, { type: "solid", pt: 1.25, color: C.ink }, none] } }))];
    rows.forEach(([m, v, c, d]) => {
      const rej = d === "REJECTED";
      table.push([
        cell(m, { bold: true, fontSize: S.body, color: rej ? C.ink3 : C.ink }),
        cell(v, { bold: true, fontSize: 15, color: rej ? C.ink3 : C.blue }),
        cell(c, { fontSize: S.small, color: C.ink2 }),
        cell(d, { bold: true, fontSize: 9, color: rej ? C.ink3 : C.blue, charSpacing: 1 }),
      ]);
    });
    s.addTable(table, { x: 0.5, y: 1.15, w: 9.0, colW: [3.0, 1.85, 3.2, 0.95], rowH: [0.3, 0.44, 0.44, 0.44, 0.44, 0.44, 0.44, 0.44],
      fontFace: F.sans, valign: "middle", margin: [0, 0.06, 0, 0.06] });
    text(s, "**Small samples:** at about 50 prompts the noise floor was about 10 points, and our probe's 130 prompts were written by one person, so Phase 4 re-measures each method on 200+ prompts, game events included, with a control arm.",
      { x: 0.5, y: 4.6, w: 9.0, h: 0.5, size: S.body, color: C.ink2 });
    caption(s, "Rows 1–4, 7: prototype, Aug 2026 [1, App. A]; rows 5–6: our probe, Oct 2026 [1, DR-016]. qwen3.5 [9] via Ollama, greedy.");
    notes(s, [
      "SPEAKER: Hero Jaiyen · about 45 seconds",
      "",
      "- These are the methods we adopt or reject. Most were measured in our prototype; the two target checks, this month.",
      "- Schema plus examples took accuracy from 58.5 to 84.9 percent, and wrong-but-legal answers, the ones players see as bugs, fell from 21 to 4 percent.",
      "- Examples alone added 35 points, the biggest lever; action first added 11.7.",
      "- Of the two target checks, the confidence check won; checking the player's words refused too many good requests.",
      "- A reasoning field made things worse, so it's rejected too. Phase 4 re-measures all of it on 200+ prompts.",
      "",
      "HAND-OFF: \"Maxim will show how every decision is recorded and how we'll measure from here.\"",
      "",
      "If asked 'Why did reasoning hurt?': the reasoning text came before the action, and the model talked itself into 'no action'; the same failure as chat-first ordering.",
    ]);
  }

  // 16 ── Telemetry (Maxim) ─────────────────────────────────────────────────
  {
    const s = pres.addSlide();
    header(s, 17, "DESIGN MODELS · DECISION TELEMETRY", "What every decision records", TEAM.maxim);
    tag(s, "built");
    figure(s, "08-telemetry", 0.5, 1.15, 6.2, 1.654, "One DecisionResult: the decision (action, target, statement) and its telemetry fields, with planned fields in a dashed box.");
    const x = 6.95, w = 2.55;
    text(s, "Five outcome classes", { x, y: 1.2, w, h: 0.3, size: S.head, bold: true });
    const outs = [
      ["Correct", "right action and target"],
      ["Wrong but legal", "the bad one: visible misbehaviour"],
      ["Contained by guard", "the safety layer working"],
      ["Rejected when an action was expected", "too cautious"],
      ["Pipeline error", "provider failed or timed out"],
    ];
    outs.forEach(([h, b], i) => {
      const y = 1.6 + i * 0.52;
      num(s, x, y + 0.02, i + 1, 0.24, i === 1 ? C.ink : C.blue);
      text(s, `**${h}** · ${b}`, { x: x + 0.34, y, w: w - 0.34, h: 0.48, size: S.small, color: C.ink2 });
    });
    text(s, "A guard catching a bad answer counts as the safety layer working, not as a failure.", { x, y: 4.3, w, h: 0.55, size: S.cap, italic: true, color: C.ink3 });
    caption(s, "Figure 11. One decision record, as merged (#2, #3, #43; TargetsDropped from #32). Dashed: planned fields.");
    notes(s, [
      "SPEAKER: Maxim Goloubitsky · about 50 seconds",
      "",
      "- Every decision produces one record, a DecisionResult. This is merged.",
      "- Top: what the model chose, action, target and statement. Then telemetry: latency, tokens, provider, framework version, the guards that fired, and the targets the cap left out.",
      "- Outcome is filled in by the evaluation harness, in five classes rather than one percentage: correct; wrong but legal, the bug players see; contained by a guard, the safety layer working; too cautious; and pipeline errors.",
      "- The dashed box is planned: whether the output was really enforced, the confidence check's score, and memory's turns sent and dropped.",
      "",
      "HAND-OFF (to yourself): \"Those outcomes feed our evaluation.\"",
    ]);
  }

  // 17 ── Evaluation method and plan (Maxim) ─────────────────────────────────
  {
    const s = pres.addSlide();
    header(s, 18, "EVALUATION · PLAN", "Evaluation plan and status", TEAM.maxim);
    const steps = [
      ["200+ prompts", "events, reports, turns, commands"],
      ["Arm A vs. arm B", "same model, machine, session"],
      ["Classify answers", "into the five outcomes"],
      ["Report and record", "findings.md · 2nd annotator"],
    ];
    steps.forEach(([h, b], i) => {
      const x = 0.5 + i * 2.32, w = 2.0;
      s.addShape(pres.shapes.RECTANGLE, { x, y: 1.2, w, h: 0.85, fill: { color: i === 1 ? C.blueLt : C.fill }, line: { color: i === 1 ? C.blueLt : C.fill } });
      text(s, h, { x: x + 0.12, y: 1.3, w: w - 0.24, h: 0.3, size: S.head, bold: true });
      text(s, b, { x: x + 0.12, y: 1.62, w: w - 0.24, h: 0.35, size: S.small, color: C.ink2 });
      if (i < 3) s.addShape(pres.shapes.LINE, { x: x + w + 0.04, y: 1.625, w: 0.24, h: 0, line: { color: C.blue, width: 2, endArrowType: "triangle" } });
    });
    text(s, "**Rules:** control arm every time · one model loaded while timing · examples never reuse eval prompts · warm up first",
      { x: 0.5, y: 2.2, w: 9.0, h: 0.42, size: S.small, color: C.ink2 });

    // phase timeline
    const phases = [["0", "Skeleton"], ["1", "Core, actions, schema"], ["2", "Ollama, queue, memory"], ["3", "Guards"], ["4", "Evaluation harness"],
      ["5", "Editor tools"], ["6", "Cloud providers"], ["6b", "In-process model"], ["7", "Samples"], ["8", "Demo games"]];
    const ty = 3.6, x0 = 0.85, step = 0.92;
    s.addShape(pres.shapes.LINE, { x: x0, y: ty, w: step * 9, h: 0, line: { color: C.line, width: 2 } });
    s.addShape(pres.shapes.LINE, { x: x0, y: ty, w: step * 1, h: 0, line: { color: C.blue, width: 3 } });
    text(s, "COMP 490 · FALL", { x: x0 - 0.35, y: 2.85, w: step * 4 + 0.7, h: 0.2, size: 8.5, bold: true, color: C.blue, cs: 1.5, align: "center" });
    text(s, "COMP 491 · SPRING", { x: x0 + step * 5 - 0.35, y: 2.85, w: step * 4 + 0.7, h: 0.2, size: 8.5, bold: true, color: C.ink3, cs: 1.5, align: "center" });
    s.addShape(pres.shapes.LINE, { x: x0 - 0.3, y: 3.1, w: step * 4 + 0.6, h: 0, line: { color: C.blue, width: 1 } });
    s.addShape(pres.shapes.LINE, { x: x0 + step * 5 - 0.3, y: 3.1, w: step * 4 + 0.6, h: 0, line: { color: C.ink3, width: 1 } });
    phases.forEach(([n, label], i) => {
      const cx = x0 + i * step, d = 0.34;
      const done = i === 0, now = i === 1;
      s.addText(n, {
        shape: pres.shapes.OVAL, x: cx - d / 2, y: ty - d / 2, w: d, h: d,
        fill: { color: done ? C.ink : now ? C.blue : C.white }, line: { color: done ? C.ink : now ? C.blue : C.line, width: 1.5 },
        fontFace: F.sans, fontSize: 9.5, bold: true, color: done || now ? C.white : C.ink2, align: "center", valign: "middle", margin: 0,
      });
      text(s, label, { x: cx - 0.45, y: ty + 0.25, w: 0.9, h: 0.45, size: S.cap, color: now ? C.blue : C.ink2, bold: now, align: "center" });
    });
    text(s, "WE ARE HERE", { x: x0 + step - 0.6, y: ty - 0.5, w: 1.2, h: 0.2, size: 8.5, bold: true, color: C.blue, cs: 1, align: "center" });
    text(s, "**Phase 1 so far:** decision types, both action front doors, `ActionRegistry`, target discovery, availability, the few-shot builder, `AgentProfile` and telemetry are merged with EditMode tests, and the first 54 evaluation prompts are written. **Next:** schema, prompt builder and `Agent`.",
      { x: 0.5, y: 4.45, w: 9.0, h: 0.5, size: S.small, color: C.ink2 });
    caption(s, "Plan of record: build plan §3, §4 and §6.5 [1]. The demo games run alongside phases 2–8.");
    notes(s, [
      "SPEAKER: Maxim Goloubitsky · about 60 seconds",
      "",
      "- Every accuracy claim will come from this method: 200+ labelled prompts, each with the state it needs, and at least half game-triggered, events, reports and turns, because most calls come from the game. We've written the first 54.",
      "- Arm A against arm B on the same model, machine and session; each answer sorted into the five outcomes; a second person labels a subset; results go in our findings log.",
      "- Why so strict? In the prototype the baseline alone drifted from 53 to 64 percent between identical runs.",
      "- Bottom: Phase 0 is done and Phase 1 is underway. Phase 2 brings the first model call and memory. This semester ends at Phase 4, so our headline number exists by December. Next semester: editor tools, more providers, samples and polished demos.",
      "",
      "HAND-OFF (to yourself): \"Our sources are on the next slide.\"",
    ]);
  }

  // 18 ── References (Maxim) ─────────────────────────────────────────────────
  {
    const s = pres.addSlide();
    header(s, 19, "REFERENCES", "References", TEAM.maxim);
    const refs = [
      "[1] Agenerela team, \"AI Agent Framework for Unity: Build Plan,\" docs/FRAMEWORK_BUILD_PLAN.md, GitHub: agenerela/agenerela, 2026. Appendix A carries the prototype's measurements (Aug. 2026).",
      "[2] Agenerela team, Software Requirements Specification (Sep. 2026), and design figures and screen wireframes, docs/design, GitHub: agenerela/agenerela.",
      "[3] R. Kelley, \"Behavior Trees Enable Structured Programming of Language Model Agents,\" arXiv:2404.07439, Apr. 2024.",
      "[4] undreamai, \"LLMUnity: Create characters in Unity with LLMs,\" GitHub. https://github.com/undreamai/LLMUnity",
      "[5] Convai. https://convai.com (accessed Oct. 1, 2026).",
      "[6] Inworld AI. https://inworld.ai (accessed Oct. 1, 2026).",
      "[7] Ollama, \"Structured outputs,\" Dec. 6, 2024. https://ollama.com/blog/structured-outputs",
      "[8] ggml-org, \"GBNF Guide,\" llama.cpp. https://github.com/ggml-org/llama.cpp/blob/master/grammars/README.md",
      "[9] Qwen Team, Qwen3.5 model family, Ollama library. https://ollama.com/library/qwen3.5 (accessed Oct. 1, 2026).",
      "[10] Google, \"Structured outputs,\" Gemini API documentation. https://ai.google.dev/gemini-api/docs/structured-output (accessed Oct. 1, 2026).",
    ];
    const col = (list, x) => s.addText(list.map((r, i) => ({ text: r, options: { breakLine: i < list.length - 1 } })), {
      x, y: 1.2, w: 4.35, h: 3.85, fontFace: F.sans, fontSize: S.small, color: C.ink2, valign: "top", margin: 0,
      paraSpaceAfter: 9, isTextBox: true,
    });
    col(refs.slice(0, 5), 0.5);
    col(refs.slice(5), 5.15);
    caption(s, "All figures, diagrams and wireframes in this deck are our own work. Repository: github.com/agenerela/agenerela");
    notes(s, [
      "SPEAKER: Maxim Goloubitsky · about 10 seconds",
      "",
      "- Our sources: our build plan and design figures, the behaviour-tree paper, and the tools we build on. Every figure is our own.",
      "",
      "HAND-OFF: \"Thank you. We're happy to take questions.\"",
    ]);
  }

  // 19 ── Q&A ────────────────────────────────────────────────────────────────
  {
    const s = pres.addSlide();
    s.background = { color: C.dark };
    text(s, "Q & A", { x: 0.6, y: 0.5, w: 8.8, h: 0.25, size: S.kicker, bold: true, color: C.blueOnDark, cs: 2 });
    text(s, "Questions?", { x: 0.6, y: 0.85, w: 8.8, h: 0.9, size: 44, bold: true, color: C.white });
    text(s, "The model decides. Validated code executes.", { x: 0.6, y: 1.8, w: 8.8, h: 0.4, size: 16, color: C.onDark });
    const who = [
      [TEAM.yevhen, "Model providers · team lead"], [TEAM.hunter, "Agents and decision requests"], [TEAM.tim, "Action vocabulary"],
      [TEAM.tigran, "Availability and the schema"], [TEAM.hero, "Targets and grounding"], [TEAM.maxim, "Telemetry and evaluation"],
    ];
    who.forEach(([n, f], i) => {
      const x = 0.6 + (i % 3) * 2.95, y = 2.85 + Math.floor(i / 3) * 0.85;
      text(s, n, { x, y, w: 2.8, h: 0.3, size: 13, bold: true, color: C.white });
      text(s, f, { x, y: y + 0.3, w: 2.8, h: 0.28, size: S.small, color: C.onDark });
    });
    text(s, "Presenters: all six of us · github.com/agenerela/agenerela", { x: 0.6, y: 4.9, w: 8.8, h: 0.25, size: S.small, color: C.onDark2 });
    notes(s, [
      "EVERYONE · 2 to 3 minutes of questions. Whoever owns the topic answers (see the names on screen).",
      "",
      "Likely questions and short answers:",
      "- Why not a bigger model? On our prototype's 20 commands, a 2B with our checks beat a 4B without them, 95% to 65%. On our harder 130-prompt probe the 4B alone (113) beat the 2B with the check (108), and the 4B with it got 124. The checks help every model; the 2B is what fits beside a game on an 8 GB card. (Yevhen)",
      "- What if the model ignores the schema? Providers are untrusted: guards re-check every answer, and Execute re-checks again before any handler runs. (Hero / Hunter)",
      "- How fast is a decision? About 0.7 seconds for the 2B on our laptop in this month's probe; the queue serves player-facing requests first. With requests one at a time, the prototype estimated about 21 seconds per round at 30 agents. (Yevhen)",
      "- Does this replace behaviour trees? No, only the top-level selection node; a behaviour tree can call our agent. (Hunter)",
      "- Isn't this a chatbot framework? No: most calls come from the game itself, like timers, events, turn reports and behaviour-tree nodes. A player's line is one kind of input. (Hunter)",
      "- What if a player says 'attack the training thing'? The model maps it to the training dummy, and the confidence check lets it through when its odds stay on the list. No word lists to maintain. (Hero)",
      "- Why not just check the player's words? We measured it: 29 good requests refused on the 2B, and worse than no check at all on the 4B. The model's own confidence did better, 83 to 108 correct out of 130. (Hero)",
      "- How does a strategy game choose targets, with no positions? Its own target source, or a list it supplies: the countries it borders or has met. The radius query is just one option for scene agents. (Hero)",
      "- Does an agent remember earlier turns? Short-term memory is a Phase 2 deliverable: the last few turns, recorded after the guards, one memory per agent, capped by tokens. Long-term memory fits the same interface later. (Hunter)",
      "- What's built today? Phase 1's decision types, action registry and both front doors, target discovery, availability, the few-shot builder and telemetry, with EditMode tests, and the first 54 evaluation prompts. Nothing calls a model yet; that's Phase 2. (Maxim)",
      "- How do you know an improvement is real? A/B with a control arm on the same model, machine and session; about a 10-point noise floor at 50 prompts, so Phase 4 uses 200+. (Maxim)",
    ]);
  }

  await pres.writeFile({ fileName: OUT });
  console.log("wrote " + OUT);
}

build().catch((e) => { console.error(e); process.exit(1); });
