// Builds Agenerela-Final-Presentation.pptx: the COMP 490 final presentation, 7 to 11 December 2026.
//
// Needs Node with pptxgenjs, react, react-dom, react-icons and sharp installed somewhere on
// NODE_PATH (they are not project dependencies):
//     npm install pptxgenjs react react-dom react-icons sharp      (in any scratch folder)
//     NODE_PATH=<that folder>/node_modules node build-deck.js
//
// The style is our October deck's (../2026-10-15-design-presentation/build-deck.js). The course
// template's design is not used; its instructions are, and the README lists each one with the
// slide that meets it. Fonts are Arial and Courier New only,
// because the team edits the deck in Google Slides. Every figure except the game frame is drawn
// here as shapes and charts, so it stays editable after the import.
//
// Every number on a slide is in numbers.md beside this file, with the command or page it came
// from. Change it there and here together.

const path = require("path");
const pptxgen = require("pptxgenjs");
const React = require("react");
const RDS = require("react-dom/server");
const sharp = require("sharp");
const fa = require("react-icons/fa6");

const HERE = __dirname;
const FIG = (n) => path.join(HERE, "figures", n);
const OUT = process.env.DECK_OUT || path.join(HERE, "Agenerela-Final-Presentation.pptx");

// ---------- design tokens ----------
const C = {
  ink: "111111", ink2: "333333", ink3: "595959", line: "BFBFBF", rule: "D9D9D9",
  fill: "F1F3F7", blue: "1F5FD1", blueLt: "E3ECFC", blueDk: "123C8C", white: "FFFFFF",
  dark: "1C1C1C", dark2: "2A2D33", onDark: "D5D8DE", onDark2: "A3A8B0", blueOnDark: "9DBDF7",
  green: "1E7B45", greenLt: "E2F2E8", amber: "8A5A00", amberLt: "FCF0D9", grey: "6B6B6B", greyLt: "EEEEEE",
};
const F = { sans: "Arial", mono: "Courier New" };
// One type scale for every slide (rubric: consistent font size). Nothing below 10 pt.
const S = { title: 26, kicker: 11, head: 16, body: 14, table: 12, small: 12, cap: 11, src: 10 };

const TEAM = ["Yevhen Mishchenko", "Hunter Hudson", "Timothy Brustinov", "Tigran Kolsuzyan", "Hero Jaiyen", "Maxim Goloubitsky"];
const TBD = "TBD";
let TOTAL = 0; // set before the slides are drawn

const pres = new pptxgen();
pres.layout = "LAYOUT_16x9"; // 10 x 5.625 in, the template's size
pres.title = "Agenerela: COMP 490 Final Presentation";
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

// Bulleted list: items are strings ("**bold** rest") or [string, level].
function bullets(s, items, o) {
  const paras = [];
  items.forEach((it, i) => {
    const [str, lvl] = Array.isArray(it) ? it : [it, 0];
    const base = { fontFace: F.sans, fontSize: lvl ? (o.size2 || S.small) : (o.size || S.body), color: lvl ? C.ink2 : C.ink };
    const r = runs(str, base);
    r[0].options = { ...r[0].options, bullet: lvl ? { indent: 14 } : { indent: 16 }, indentLevel: lvl, paraSpaceBefore: i && !lvl ? (o.gap ?? 6) : 2 };
    r[r.length - 1].options = { ...r[r.length - 1].options, breakLine: i < items.length - 1 };
    paras.push(...r);
  });
  s.addText(paras, { x: o.x, y: o.y, w: o.w, h: o.h, margin: 0, valign: "top", isTextBox: true });
}

let slideNo = 0;
function header(s, kicker, title, part) {
  slideNo++;
  s.background = { color: C.white };
  text(s, kicker, { x: 0.4, y: 0.24, w: 6.6, h: 0.22, size: S.kicker, bold: true, color: C.blue, cs: 1.5 });
  text(s, title, { x: 0.4, y: 0.47, w: 6.9, h: 0.55, size: S.title, bold: true });
  if (part) {
    s.addText([{ text: "Presenter: ", options: { color: C.ink3 } }, { text: TBD, options: { bold: true, color: C.ink } },
               { text: `  ·  part ${part}`, options: { color: C.ink3 } }],
      { shape: pres.shapes.ROUNDED_RECTANGLE, rectRadius: 0.15, x: 7.35, y: 0.2, w: 2.3, h: 0.32,
        fill: { color: C.fill }, line: { color: C.fill }, fontFace: F.sans, fontSize: S.cap, align: "center", valign: "middle", margin: 0 });
  }
  text(s, `${slideNo} / ${TOTAL}`, { x: 8.85, y: 5.3, w: 0.8, h: 0.2, size: S.src, color: C.ink3, align: "right" });
}

// Source and caption line at the bottom (template: "Source: ...").
function source(s, str) {
  text(s, str, { x: 0.4, y: 5.12, w: 8.35, h: 0.4, size: S.src, italic: true, color: C.ink3, valign: "bottom" });
}

// Status of what the slide shows: built, work (In work), planned, partly.
const STATUS = {
  built: ["Built", C.green, C.white, C.green, null],
  work: ["In work", C.white, C.blue, C.blue, null],
  partly: ["Partly", C.white, C.green, C.green, null],
  planned: ["Planned", C.white, C.grey, C.grey, "dash"],
};
function tag(s, status, x, y, w = 0.85, h = 0.26) {
  const [label, fill, color, line, dash] = STATUS[status];
  s.addText(label, {
    shape: pres.shapes.ROUNDED_RECTANGLE, rectRadius: 0.12, x, y, w, h,
    fill: { color: fill }, line: { color: line, width: 1.25, dashType: dash || "solid" },
    fontFace: F.sans, fontSize: S.cap, bold: true, color, align: "center", valign: "middle", margin: 0,
  });
}

function num(s, x, y, n, d = 0.3, fill = C.blue, color = C.white) {
  s.addText(String(n), {
    shape: pres.shapes.OVAL, x, y, w: d, h: d, fill: { color: fill }, line: { color: fill },
    fontFace: F.sans, fontSize: 12, bold: true, color, align: "center", valign: "middle", margin: 0,
  });
}

function box(s, x, y, w, h, fill = C.fill, line = null, dash = null, r = 0.06) {
  s.addShape(pres.shapes.ROUNDED_RECTANGLE, { x, y, w, h, rectRadius: r, fill: { color: fill },
    line: line ? { color: line, width: 1.25, dashType: dash || "solid" } : { color: fill } });
}

function arrow(s, x1, y1, x2, y2, color = C.ink3, width = 1.75, dash) {
  const flipH = x2 < x1, flipV = y2 < y1;
  s.addShape(pres.shapes.LINE, { x: Math.min(x1, x2), y: Math.min(y1, y2), w: Math.abs(x2 - x1), h: Math.abs(y2 - y1), flipH, flipV,
    line: { color, width, endArrowType: "triangle", dashType: dash || "solid" } });
}

// A table with a dark header row. rows: arrays of strings ("**bold**" works).
function table(s, head, rows, o) {
  const fs = o.size || S.table;
  const cell = (str, extra = {}) => ({ text: runs(String(str), { fontFace: F.sans, fontSize: fs, color: C.ink }), options: { valign: "middle", ...extra } });
  // A status cell: { status: "built" } shows the status word in its colour, so it moves with the row.
  const statusCell = (st, extra) => {
    const [label, fill, color, line] = STATUS[st];
    const solid = fill !== C.white;
    return { text: label, options: { ...extra, bold: true, align: "center", valign: "middle", fontFace: F.sans, fontSize: fs,
      color: solid ? C.white : line, fill: { color: solid ? fill : extra.fill.color } } };
  };
  const data = [head.map((h) => ({ text: h, options: { bold: true, color: C.white, fill: { color: C.dark2 }, fontFace: F.sans, fontSize: fs, valign: "middle" } }))];
  rows.forEach((r, i) => data.push(r.map((c) => {
    const extra = { fill: { color: i % 2 ? C.white : C.fill } };
    return c && c.status ? statusCell(c.status, extra) : cell(c, extra);
  })));
  s.addTable(data, { x: o.x, y: o.y, w: o.w, colW: o.colW, rowH: o.rowH || 0.3, border: { type: "solid", pt: 0.5, color: C.rule }, margin: o.margin || [3, 6, 3, 6], autoPage: false });
}

async function icon(Comp, color = "#FFFFFF") {
  const svg = RDS.renderToStaticMarkup(React.createElement(Comp, { color, size: "256" }));
  const buf = await sharp(Buffer.from(svg)).png().toBuffer();
  return "image/png;base64," + buf.toString("base64");
}
async function iconDot(s, x, y, d, data, fill = C.blue) {
  s.addShape(pres.shapes.OVAL, { x, y, w: d, h: d, fill: { color: fill }, line: { color: fill } });
  const pad = d * 0.24;
  s.addImage({ data, x: x + pad, y: y + pad, w: d - 2 * pad, h: d - 2 * pad });
}

const notes = (s, lines) => s.addNotes(lines.join("\n"));

// ---------- the parts, for the outline and the notes ----------
// Six parts of roughly three minutes; part D is the recorded demo.
const PARTS = [
  ["A", "Introduction", "1–5", "3:15"],
  ["B", "Requirements: then and now", "6–8", "2:10"],
  ["C", "Design updates", "9–11", "1:55"],
  ["D", "Demo: Painted Realm (video ≈ 5:00)", "12–14", "6:10"],
  ["E", "GitHub repository", "15–17", "2:15"],
  ["F", "Group management, thanks", "18–21", "2:35"],
];

// ---------- slides ----------
async function build() {
  TOTAL = 23;
  const ic = {
    pen: await icon(fa.FaPenToSquare), shield: await icon(fa.FaShieldHalved), cloud: await icon(fa.FaCloud),
    repeat: await icon(fa.FaRepeat), check: await icon(fa.FaCheck), play: await icon(fa.FaPlay),
  };

  // 1 ── Title ───────────────────────────────────────────────────────────────
  {
    const s = pres.addSlide();
    slideNo++;
    s.background = { color: C.dark };
    text(s, "COMP 490 SENIOR DESIGN PROJECT · FINAL PRESENTATION", { x: 0.6, y: 0.45, w: 8.8, h: 0.24, size: S.kicker, bold: true, color: C.blueOnDark, cs: 2 });
    text(s, "Project Report for Agenerela", { x: 0.6, y: 0.78, w: 8.8, h: 0.75, size: 40, bold: true, color: C.white });
    text(s, "A Unity framework: a language model chooses, validated game code acts",
      { x: 0.6, y: 1.55, w: 8.8, h: 0.35, size: 18, color: C.onDark });

    const by = 2.25, bh = 0.85;
    box(s, 0.6, by, 2.4, bh, C.dark2, "6B7078");
    text(s, "YOUR GAME", { x: 0.75, y: by + 0.14, w: 2.1, h: 0.25, size: 13, bold: true, color: C.white, cs: 2 });
    text(s, "Asks, then acts", { x: 0.75, y: by + 0.46, w: 2.1, h: 0.28, size: S.body, color: C.onDark });
    arrow(s, 3.05, by + bh / 2, 3.5, by + bh / 2, C.onDark, 1.75);
    box(s, 3.55, by, 2.9, bh, C.blue);
    text(s, "AGENERELA", { x: 3.7, y: by + 0.14, w: 2.6, h: 0.25, size: 13, bold: true, color: C.white, cs: 2 });
    text(s, "Offers legal options, checks", { x: 3.7, y: by + 0.46, w: 2.7, h: 0.28, size: S.body, color: C.white });
    arrow(s, 6.5, by + 0.3, 6.95, by + 0.3, C.onDark, 1.75);
    arrow(s, 6.95, by + 0.56, 6.5, by + 0.56, C.onDark, 1.75);
    box(s, 7.0, by, 2.4, bh, C.dark, C.onDark2, "dash");
    text(s, "THE MODEL", { x: 7.15, y: by + 0.14, w: 2.1, h: 0.25, size: 13, bold: true, color: C.white, cs: 2 });
    text(s, "Chooses, never acts", { x: 7.15, y: by + 0.46, w: 2.2, h: 0.28, size: S.body, color: C.onDark });

    text(s, `${TEAM[0]}  ·  ${TEAM[1]}  ·  ${TEAM[2]}`, { x: 0.6, y: 3.5, w: 8.8, h: 0.3, size: 15, bold: true, color: C.white });
    text(s, `${TEAM[3]}  ·  ${TEAM[4]}  ·  ${TEAM[5]}`, { x: 0.6, y: 3.82, w: 8.8, h: 0.3, size: 15, bold: true, color: C.white });
    text(s, "Department of Computer Science", { x: 0.6, y: 4.3, w: 8.8, h: 0.26, size: 14, color: C.onDark });
    text(s, "California State University, Northridge", { x: 0.6, y: 4.58, w: 8.8, h: 0.26, size: 14, color: C.onDark });
    text(s, "December 2026  ·  github.com/agenerela/agenerela", { x: 0.6, y: 4.98, w: 8.8, h: 0.24, size: S.small, color: C.onDark2 });
    notes(s, [
      "Speaking notes · Presenter TBD (part A) · about 20 seconds",
      "",
      "- Hello, we're the Agenerela team: Yevhen, Hunter, Tim, Tigran, Hero and Maxim.",
      "- Agenerela is a Unity framework that lets a language model choose what a game agent does next, only from actions the developer registered, while the game's own code carries it out.",
    ]);
  }

  // 2 ── Outline and who presents what ───────────────────────────────────────
  {
    const s = pres.addSlide();
    header(s, "OUTLINE", "Outline, and who presents what", "A");
    table(s, ["Part", "Section", "Slides", "Time", "Presenter"],
      PARTS.map(([p, n, sl, t]) => [`**${p}**`, n, sl, t, "TBD"]),
      { x: 0.4, y: 1.2, w: 6.0, colW: [0.55, 3.0, 0.8, 0.7, 0.95], rowH: 0.42, size: S.table });
    text(s, "Total ≈ 18:20, inside the 15 to 21 minutes. Questions after slide 23: 3 minutes, everyone.",
      { x: 0.4, y: 4.3, w: 6.0, h: 0.5, size: S.small, color: C.ink2 });

    box(s, 6.75, 1.2, 2.9, 3.55, C.blueLt);
    text(s, "HOW WE SPLIT IT", { x: 6.95, y: 1.35, w: 2.6, h: 0.22, size: S.cap, bold: true, color: C.blueDk, cs: 1.2 });
    bullets(s, [
      "Six parts, about **2½ to 3 minutes** of speaking each",
      "Part D's presenter frames the **5-minute video** and plays it",
      "Every slide names its presenter; **TBD** until the team decides",
      "Each speaker hands off by name to the next",
    ], { x: 6.95, y: 1.68, w: 2.55, h: 2.95, size: S.small, gap: 8 });
    source(s, "Times are the speaker notes' estimates at about 140 words a minute; the video's length is the demo session's target.");
    notes(s, [
      "Speaking notes · Presenter TBD (part A) · about 20 seconds",
      "",
      "- Six parts: what Agenerela is and why, how our requirements changed, how the design changed, a five-minute recorded demo of our strategy game, how we used GitHub, and how we managed the group.",
      "- (Point at the table; don't read it.) Each of us takes one part.",
      "",
      "TEAM NOTE: fill in the Presenter column and the 'Presenter: TBD' pill on each slide once the split is decided. Parts A, B, C, E and F are about 1:55 to 3:15 each; part D is about 70 seconds of speaking plus the video. If the video runs long, cut slide 14 to one line.",
    ]);
  }

  // 3 ── Introduction: the problem, for Unity developers ─────────────────────
  {
    const s = pres.addSlide();
    header(s, "1 · INTRODUCTION · BACKGROUND", "The problem, for Unity developers", "A");
    box(s, 0.4, 1.15, 5.4, 0.95, C.blueLt);
    text(s, "A Unity developer who wants agents to react to open-ended input has **no safe, reusable way** to let a language model decide what they do.",
      { x: 0.6, y: 1.2, w: 5.05, h: 0.85, size: S.body, valign: "middle" });
    text(s, "WITHOUT A FRAMEWORK, EVERY DEVELOPER HAS TO", { x: 0.4, y: 2.28, w: 5.4, h: 0.22, size: S.cap, bold: true, color: C.ink3, cs: 1.2 });
    const pains = [
      [ic.pen, "Write prompts and parse replies", "For every agent, then turn free text back into code"],
      [ic.shield, "Trust whatever comes back", "An action that isn't possible, a target that isn't there"],
      [ic.cloud, "Pay per request, or ship a server", "Players won't install a model server"],
      [ic.repeat, "Start over for each kind of agent", "A guard, a companion and a country, wired differently"],
    ];
    for (let i = 0; i < 4; i++) {
      const [img, h, b] = pains[i];
      const y = 2.6 + i * 0.6;
      await iconDot(s, 0.4, y, 0.4, img);
      text(s, h, { x: 0.95, y: y - 0.04, w: 4.85, h: 0.28, size: S.body, bold: true });
      text(s, b, { x: 0.95, y: y + 0.23, w: 4.85, h: 0.26, size: S.small, color: C.ink2 });
    }
    const px = 6.1, pw = 3.55;
    box(s, px, 1.15, pw, 3.85, C.dark, null, null, 0.04);
    text(s, "WITH AGENERELA", { x: px + 0.25, y: 1.32, w: pw - 0.5, h: 0.22, size: S.cap, bold: true, color: C.blueOnDark, cs: 1.2 });
    const gains = ["Register actions, not prompts", "Only legal, grounded answers reach your code", "A local model, no per-request bill", "One API for any agent, with or without a body"];
    for (let i = 0; i < 4; i++) {
      const y = 1.68 + i * 0.5;
      await iconDot(s, px + 0.25, y, 0.28, ic.check);
      text(s, gains[i], { x: px + 0.68, y: y - 0.03, w: pw - 0.85, h: 0.5, size: S.body, color: C.white });
    }
    s.addShape(pres.shapes.LINE, { x: px + 0.25, y: 3.72, w: pw - 0.5, h: 0, line: { color: "4A4F57", width: 1 } });
    text(s, "**Our prototype, a 2B model:** 58.5% → **84.9%** correct with a legal-options schema and examples (A/B, n = 53 an arm). A cloud API for 30 agents: **≈ $165** per 100 hours of play.",
      { x: px + 0.25, y: 3.8, w: pw - 0.45, h: 1.15, size: S.small, color: C.onDark });
    source(s, "Source: build plan §0, §5 and Appendix A, prototype of Aug 2026, qwen3.5 via Ollama; noise floor ≈ 10 points at n = 53; cost at Flash-Lite pricing, 6 decisions a minute [1].");
    notes(s, [
      "Speaking notes · Presenter TBD (part A) · about 60 seconds",
      "",
      "- Our users are Unity developers. Say one wants a guard, a companion or a whole country that reacts to things nobody scripted: a player's typed line, a war report.",
      "- Today they have no safe, reusable way to let a language model decide what that agent does. (Read the blue box once.)",
      "- Without a framework, each developer rebuilds the same plumbing for every agent, and still can't trust the answer.",
      "- Our prototype showed why it matters: a small model went from 58.5 to 84.9 percent correct just from offering legal options and examples, and a cloud API for thirty agents would cost about 165 dollars per hundred hours of play.",
      "- Agenerela turns that around: they register actions, only legal answers reach their code, it runs on a local model, and one API covers any agent, even one with no body.",
      "",
      "If asked 'Why not a cloud model?': Agenerela can use one through the same provider interface (Phase 6, 2027). Local first, because a shipped game can't send every decision to a paid API.",
      "If asked 'Does it replace behaviour trees?': No. Only the top-level choice; the game's own code still does the how, every frame.",
    ]);
  }

  // 4 ── Introduction: how it works ──────────────────────────────────────────
  {
    const s = pres.addSlide();
    header(s, "1 · INTRODUCTION · BACKGROUND KNOWLEDGE", "One decision, start to finish", "A");
    const y0 = 1.25, h0 = 2.55;
    // the game
    box(s, 0.4, y0, 2.2, h0, C.white, C.ink2);
    text(s, "YOUR GAME", { x: 0.55, y: y0 + 0.12, w: 1.9, h: 0.22, size: S.cap, bold: true, color: C.ink3, cs: 1.5 });
    num(s, 0.55, y0 + 0.45, 1);
    text(s, "**Asks** an agent, when its own systems decide", { x: 0.95, y: y0 + 0.42, w: 1.55, h: 0.75, size: S.small });
    num(s, 0.55, y0 + 1.6, 5);
    text(s, "**Runs** the chosen action in its own handler", { x: 0.95, y: y0 + 1.57, w: 1.55, h: 0.75, size: S.small });
    // agenerela
    box(s, 3.0, y0, 3.9, h0, C.blue);
    text(s, "AGENERELA", { x: 3.15, y: y0 + 0.12, w: 3.5, h: 0.22, size: S.cap, bold: true, color: C.white, cs: 1.5 });
    const steps = [
      [2, "Offers only what is legal now", "Actions from the agent's state, targets it can see"],
      [4, "Checks the answer", "Guards turn an illegal or ungrounded choice into none"],
    ];
    steps.forEach(([n, h, b], i) => {
      const yy = y0 + 0.45 + i * 1.15;
      num(s, 3.15, yy, n, 0.3, C.white, C.blue);
      text(s, h, { x: 3.55, y: yy - 0.02, w: 3.25, h: 0.3, size: S.body, bold: true, color: C.white });
      text(s, b, { x: 3.55, y: yy + 0.3, w: 3.25, h: 0.5, size: S.small, color: C.white });
    });
    // the model
    box(s, 7.3, y0, 2.35, h0, C.white, C.ink3, "dash");
    text(s, "THE MODEL", { x: 7.45, y: y0 + 0.12, w: 2.0, h: 0.22, size: S.cap, bold: true, color: C.ink3, cs: 1.5 });
    num(s, 7.45, y0 + 0.45, 3);
    text(s, "**Chooses** one action, one target and a line to say", { x: 7.85, y: y0 + 0.42, w: 1.7, h: 0.9, size: S.small });
    text(s, "Local, through Ollama; never touches the game", { x: 7.45, y: y0 + 1.6, w: 2.1, h: 0.7, size: S.small, italic: true, color: C.ink3 });
    // arrows
    arrow(s, 2.62, y0 + 0.6, 2.98, y0 + 0.6, C.blue, 2.25);
    arrow(s, 6.92, y0 + 0.6, 7.28, y0 + 0.6, C.blue, 2.25);
    arrow(s, 7.28, y0 + 1.75, 6.92, y0 + 1.75, C.blue, 2.25);
    arrow(s, 2.98, y0 + 1.75, 2.62, y0 + 1.75, C.blue, 2.25);
    // example strip
    box(s, 0.4, 4.0, 9.25, 0.95, C.fill);
    text(s, "IN OUR STRATEGY GAME", { x: 0.55, y: 4.08, w: 3, h: 0.2, size: S.cap, bold: true, color: C.ink3, cs: 1.2 });
    text(s, "**Asked:** a ruler's turn and what it sees  →  **offered:** fortify, recruit, declare war on a neighbour, …  →  **chosen:** one order and a line for the chronicle  →  **checked**, then the game carries it out",
      { x: 0.55, y: 4.32, w: 8.95, h: 0.58, size: S.small, color: C.ink2 });
    source(s, "Figure 1. One decision through Agenerela; our drawing of the build plan's architecture [1, §2]. The strategy example is illustrative of Painted Realm's court orders.");
    notes(s, [
      "Speaking notes · Presenter TBD (part A) · about 50 seconds",
      "",
      "- Here's one decision, in five steps. (Follow the numbers.)",
      "- One: the game asks an agent, whenever its own systems decide to. We never decide when or why an agent is asked; that stays the developer's.",
      "- Two: Agenerela offers the model only what is legal right now. If a country has no neighbour, 'declare war' isn't even in the list.",
      "- Three: the model chooses one action, one target and a line to say. It's untrusted, and it never touches the game.",
      "- Four: we check the answer. A target it isn't sure of, or an action that isn't allowed, becomes 'none'.",
      "- Five: the game runs the action in its own code.",
      "",
      "If asked 'What if the model writes something outside the list?': The request carries a JSON schema whose enums hold only the legal options, so the model can't write anything else; and the guards check again anyway.",
    ]);
  }

  // 5 ── Goals and approach ──────────────────────────────────────────────────
  {
    const s = pres.addSlide();
    header(s, "1 · INTRODUCTION · GOALS AND APPROACH", "Goals and where they stand", "A");
    const goals = [
      ["Any game entity can be an agent", "built"], ["Developers define the actions", "built"],
      ["Only legal actions, no made-up targets", "built"], ["Agents remember what happened", "built"],
      ["Any model, even inside the game", "partly"], ["The game stays responsive", "built"],
      ["Every accuracy claim is measured", "work"], ["A working agent in under 15 minutes", "planned"],
    ];
    goals.forEach(([g, st], i) => {
      const col = i % 2, row = Math.floor(i / 2);
      const x = 0.4 + col * 4.65, y = 1.2 + row * 0.62;
      box(s, x, y, 4.5, 0.52, C.fill);
      num(s, x + 0.12, y + 0.11, i + 1);
      text(s, g, { x: x + 0.55, y, w: 2.9, h: 0.52, size: S.body, valign: "middle" });
      tag(s, st, x + 3.5, y + 0.13);
    });
    box(s, 0.4, 3.78, 9.25, 1.0, C.blueLt);
    text(s, "**Approach.** The framework is the scaffolding around a small, swappable model: legal options in the request, checks on the answer, and an A/B measurement before any claim. Built in phases, each with a gate of tests we must pass.",
      { x: 0.6, y: 3.83, w: 8.9, h: 0.9, size: S.body, valign: "middle" });
    source(s, "Goals from our system design (24 Sep 2026) and October deck; status on test, 24 Nov 2026: 5 partly = Ollama only, 7 = Phase 4, 8 = Phase 5 [1, §3].");
    notes(s, [
      "Speaking notes · Presenter TBD (part A) · about 45 seconds",
      "",
      "- These are the eight goals we set in September. Six are built.",
      "- 'Any model' is partly there: Ollama works today; the in-process model a player could ship, and cloud providers, are 2027 phases.",
      "- 'Every claim measured' is Phase 4, our evaluation harness, due 7 December. 'A working agent in 15 minutes' is the editor tooling of Phase 5.",
      "- Our approach in one line: the model is a swappable commodity, the scaffolding around it is the product, and nothing counts until an A/B run with a control says so.",
      "",
      "HAND-OFF: \"<next presenter> will walk you through how our requirements changed since September.\"",
      "",
      "If asked 'What is a phase gate?': A GitHub issue listing the tests a phase must pass before it closes; Phase 3's, for example, needed 19 of 20 on the prototype's suite with a 2B model.",
    ]);
  }

  // 6 ── Requirements: the initial version ───────────────────────────────────
  {
    const s = pres.addSlide();
    header(s, "2 · REQUIREMENTS · INITIAL VERSION", "Our six starting requirements", "B");
    const rows = [
      ["3.1", "**Agents and decision requests**", "Hunter Hudson", "#2", "built"],
      ["3.2", "**Developer-authored action vocabulary**", "Timothy Brustinov", "#4, #15, #49", "built"],
      ["3.3", "**Action availability and the decision schema**", "Tigran Kolsuzyan", "#6, #7, #8", "built"],
      ["3.4", "**Target registry and target grounding**", "Hero Jaiyen", "#5, #32, #81", "built"],
      ["3.5", "**Swappable language-model providers**", "Yevhen Mishchenko", "Phase 2", "partly"],
      ["3.6", "**Decision telemetry and outcome classes**", "Maxim Goloubitsky", "#3, #43", "built"],
    ];
    table(s, ["SRS", "Requirement (one feature each)", "Owner", "Issues", "Status, 24 Nov"],
      rows.map((r) => [r[0], r[1], r[2], r[3], { status: r[4] }]),
      { x: 0.4, y: 1.15, w: 9.25, colW: [0.55, 4.0, 1.85, 1.35, 1.5], rowH: 0.42, size: S.table });
    text(s, "3.5 is partly built: the Ollama provider passed its Phase 2 gate on 25 October; cloud providers (Phase 6) and a model inside the shipped game (Phase 6b) are 2027 work.",
      { x: 0.4, y: 4.2, w: 9.25, h: 0.55, size: S.small, color: C.ink2 });
    source(s, "Source: Software Requirements Specification, 10 Sep 2026 (one feature per member) and build plan §6.6 [1, 2]; issue numbers from GitHub.");
    notes(s, [
      "Speaking notes · Presenter TBD (part B) · about 35 seconds",
      "",
      "- In September each of us owned one major requirement in our requirements specification. These six are the initial version.",
      "- They cover how a game asks, what an agent can do, which options are legal, what an action aims at, which model answers, and what gets recorded.",
      "- Five of the six are built and tested. Providers is partly built: Ollama works, which is how everything in the demo runs; cloud providers and a model inside the shipped game are next year's phases.",
      "",
      "If asked 'Why is the in-process provider so late?': It needs a grammar serializer and a model a player can legally ship. The plan puts the evaluation harness first, so we can measure whether it matches Ollama's accuracy.",
    ]);
  }

  // 7 ── Requirements added since ────────────────────────────────────────────
  {
    const s = pres.addSlide();
    header(s, "2 · REQUIREMENTS · ADDED SINCE", "Requirements we added, and why", "B");
    const rows = [
      ["**Observations and memory**", "Knows what it sees and what it just did", "DR-012 · #90, #91", "built"],
      ["**Outcomes**", "Memory records what really happened", "DR-018 · PR #195", "built"],
      ["**Decision rounds**", "Many agents decide, then act in your order", "DR-021 · PR #243", "built"],
      ["**Typed parameters**", "Numbers and choices beside the target", "DR-023 · PR #342", "built"],
      ["**Guards**", "Checks every answer; add your own", "Phase 3 · #106–#109", "built"],
      ["**Evaluation harness**", "209 labelled prompts and A/B runs", "Phase 4 · #145, #147", "work"],
      ["**Prompt budget**", "Measures prompts; trims only what you list", "#151 · PR #392", "built"],
      ["**Provider retry, profiles**", "Waits out a busy server; settings profiles", "DR-026 · PR #395", "built"],
      ["**Image provider**", "An action can ask for a picture; cached", "DR-025 · PR #394", "built"],
      ["**Speech provider**", "A line read aloud by a voice provider", "DR-024, not built yet", "planned"],
    ];
    table(s, ["New requirement", "What it gives the developer", "Where it is", "Status"],
      rows.map((r) => [r[0], r[1], r[2], { status: r[3] }]),
      { x: 0.4, y: 1.12, w: 9.25, colW: [2.6, 3.55, 2.1, 1.0], rowH: 0.33, size: S.table });
    source(s, "Framework features only; the games' own features are not listed. Status on test, 24 Nov 2026. Sources: build plan §2.8, §2.9, §3, DR-012, DR-018, DR-021, DR-023 to DR-026 [1]; GitHub (numbers.md).");
    notes(s, [
      "Speaking notes · Presenter TBD (part B) · about 50 seconds",
      "",
      "- These are framework requirements added after September: features any game can use. Each came because a demo game or a measurement showed the API was missing it.",
      "- The game now reports what its action actually did, and memory records that. Many agents can decide on one snapshot and then act in an order the game sets. Actions can take typed parameters, like a number of troops, beside the target.",
      "- Guards became a whole pipeline, and developers can add their own rules. That was Phase 3, closed 3 November.",
      "- The evaluation harness: 209 labelled prompts and an A/B runner. It's Phase 4, due 7 December; what's left is a second person labelling a subset blind.",
      "- The prompt budget merged today, 24 November: the framework measures every prompt and trims only what the developer marked optional.",
      "- Provider retries and settings profiles merged today: a provider waits out a busy server, and a game switches between development and production settings. The image provider merged today too: an action can ask for a picture, and results are cached. A speech provider is decided (DR-024) but not built.",
      "",
      "TEAM NOTE: re-check the In work and Planned rows before presenting; they will move before 7 December.",
      "If asked 'How did you decide what to add?': From the demo games. The gaps they hit are logged in issue #190 and became framework features.",
    ]);
  }

  // 8 ── Requirements: measured ──────────────────────────────────────────────
  {
    const s = pres.addSlide();
    header(s, "2 · REQUIREMENTS · MEASURED", "What we measured", "B");
    const rows = [
      ["Phase 2 gate, 25 Oct", "Live decision through Unity, `qwen3.5:2b`, RTX 5080", "0.23 to 0.56 s a warm decision (bar: under 5 s)", "Latency, not accuracy"],
      ["Phase 3 gate, 3 Nov", "Guards on against the check off, 20 prompts, `qwen3.5:2b`", "**19/20** against 18/20; impossible requests 6/6 against 5/6", "n = 20: one answer, far inside noise; the gate's bar was 19/20"],
      ["Phase 4 gate A/B, 23 Nov", "Same A/B on our 209-prompt set, `qwen3.5:2b`", "66.0% against 67.9%; 14 wrong actions stopped, 4 right ones lost", "+1.9 points, inside the 4.9-point noise floor: no claim"],
      ["Prompt budget, 24 Nov", "Prompt size against Ollama's token count, 6,304 logged decisions", "3.10 characters a token (sd 0.05)", "No model run: a calibration"],
    ];
    table(s, ["Run", "What was compared", "Result", "What we can claim"], rows,
      { x: 0.4, y: 1.12, w: 9.25, colW: [1.55, 2.9, 2.6, 2.2], rowH: 0.62, size: S.table });
    box(s, 0.4, 4.3, 9.25, 0.6, C.blueLt);
    text(s, "**Our rule:** no accuracy claim without an A/B run: same model, machine, prompts and session, with a control arm.",
      { x: 0.6, y: 4.3, w: 8.9, h: 0.6, size: S.body, valign: "middle" });
    source(s, "Source: findings.md, entries of 25 Oct, 3 Nov, 23 Nov and 24 Nov 2026 [3]; build plan §3 [1]. Noise floor as findings computes it for n = 209.");
    notes(s, [
      "Speaking notes · Presenter TBD (part B) · about 50 seconds",
      "",
      "- We hold ourselves to one rule: no accuracy claim without an A/B run against a control, on the same model, machine and prompts.",
      "- Phase 3's gate passed 19 of 20 with the guards on, 18 without. Twenty prompts is one answer's difference, so that's a pass, not a proof.",
      "- So in Phase 4 we ran it on our 209-prompt set: 66.0 against 67.9 percent. That difference is inside the noise, so we claim no accuracy change. What the guards do is stop wrong actions: 14 wrong ones became 'none', at the cost of 4 right ones.",
      "",
      "HAND-OFF: \"<next presenter> will show how the design changed to support all this.\"",
      "",
      "If asked 'So the guards don't help?': Not on accuracy, within noise. They change which mistakes reach the game: a wrong attack becomes doing nothing. Impossible requests, where they matter most, were only 15 of the 209, so we want more of them.",
      "If asked 'What's the noise floor?': How far two runs can differ by chance at that sample size; at 209 prompts it's 4.9 points.",
    ]);
  }

  // 9 ── Design: then and now ────────────────────────────────────────────────
  {
    const s = pres.addSlide();
    header(s, "3 · DESIGN UPDATES · ARCHITECTURE", "What the design gained", "C");
    const mods = [
      ["Agent", "who it is, the request", [["Observations", "built"], ["Memory", "built"], ["Execution outcome", "built"]]],
      ["Actions and targets", "registry, availability", [["Target categories", "built"], ["Typed parameters", "built"], ["Choice calls", "built"]]],
      ["Schema and prompt", "action → target → statement", [["Prompt budget", "built"], ["Examples with situation", "built"]]],
      ["Queue and providers", "one interface, Ollama", [["Model lifecycle", "built"], ["Retry, profiles", "built"], ["Image provider", "built"]]],
      ["Guards", "the grounding check", [["Guard pipeline", "built"], ["Your own guards", "built"]]],
      ["Result and telemetry", "outcome classes", [["Decision feed, log", "built"], ["Evaluation harness", "work"]]],
    ];
    const bw = 2.95, bh = 1.78, gx = 0.2, gy = 0.14;
    mods.forEach(([t, sept, adds], i) => {
      const col = i % 3, row = Math.floor(i / 3);
      const x = 0.4 + col * (bw + gx), y = 1.12 + row * (bh + gy);
      box(s, x, y, bw, bh, C.white, C.ink2);
      text(s, t, { x: x + 0.12, y: y + 0.08, w: bw - 0.24, h: 0.28, size: S.body, bold: true });
      text(s, "Sept: " + sept[0].toUpperCase() + sept.slice(1), { x: x + 0.12, y: y + 0.38, w: bw - 0.24, h: 0.24, size: S.cap, color: C.ink3 });
      adds.forEach(([a, st], j) => {
        const yy = y + 0.68 + j * 0.36;
        const isNew = st === "built";
        s.addText("+ " + a, { shape: pres.shapes.ROUNDED_RECTANGLE, rectRadius: 0.05, x: x + 0.12, y: yy, w: bw - 0.24, h: 0.3,
          fill: { color: isNew ? C.blue : C.white }, line: { color: C.blue, width: 1.25, dashType: isNew ? "solid" : "dash" },
          fontFace: F.sans, fontSize: S.small, color: isNew ? C.white : C.blue, bold: true, valign: "middle", margin: [0, 6, 0, 6], fit: "none" });
      });
      if (i < 5 && col < 2) arrow(s, x + bw + 0.01, y + bh / 2, x + bw + gx - 0.01, y + bh / 2, C.ink3, 1.5);
    });
    // legend
    s.addText("+ Added, built", { shape: pres.shapes.ROUNDED_RECTANGLE, rectRadius: 0.05, x: 0.4, y: 4.88, w: 1.45, h: 0.26, fill: { color: C.blue }, line: { color: C.blue }, fontFace: F.sans, fontSize: S.cap, color: C.white, bold: true, align: "center", valign: "middle", margin: 0 });
    s.addText("+ Added, in work", { shape: pres.shapes.ROUNDED_RECTANGLE, rectRadius: 0.05, x: 1.95, y: 4.88, w: 1.55, h: 0.26, fill: { color: C.white }, line: { color: C.blue, dashType: "dash", width: 1.25 }, fontFace: F.sans, fontSize: S.cap, color: C.blue, bold: true, align: "center", valign: "middle", margin: 0 });
    text(s, "Black: September. Framework features only.", { x: 3.65, y: 4.88, w: 6.0, h: 0.26, size: S.cap, color: C.ink3, valign: "middle" });
    source(s, "Figure 2. Agenerela's modules then and now, our own drawing from build plan §2 and DR-012 to DR-026 [1]; status on test, 24 Nov 2026.");
    notes(s, [
      "Speaking notes · Presenter TBD (part C) · about 40 seconds",
      "",
      "- This is our design: six modules in the order a decision flows. The grey line in each is what we had in September; the blue chips are what we added. Solid is built, dashed is in work.",
      "- The agent gained observations, memory, and an outcome: the game now tells the framework what actually happened, so memory records that, not just the choice.",
      "- Providers gained model lifecycle, retries, settings profiles and an image provider; a speech provider is next.",
      "- Every one is a framework feature any game can use; each came from a demo game asking for something the API couldn't say.",
      "",
      "If asked 'Did the core change shape?': No. The game still asks, Agenerela offers legal options, the model picks, the guards check and the game acts. Everything here plugs into that loop through an interface.",
    ]);
  }

  // 10 ── Design: decision records ───────────────────────────────────────────
  {
    const s = pres.addSlide();
    header(s, "3 · DESIGN UPDATES · DECISION RECORDS", "Eighteen design decisions", "C");
    const cols = [
      ["SEPTEMBER", [
        ["DR-009", "One dependency: JSON"],
        ["DR-010", "A project per demo"],
        ["DR-011", "Actions: code or asset"],
        ["DR-012", "Agents' own observations"],
        ["DR-013", "One provider config"],
        ["DR-014", "Targets from the scene"],
        ["DR-015", "Developer picks when"],
      ]],
      ["OCTOBER", [
        ["DR-016", "Guard reads the model's confidence"],
        ["DR-017", "Prompt-set tools in package"],
      ]],
      ["NOVEMBER", [
        ["DR-018", "Game reports outcomes"],
        ["DR-019", "Optional warm-up, unload"],
        ["DR-020", "Targets have categories"],
        ["DR-021", "Decide all, then order"],
        ["DR-022", "Options with the call"],
        ["DR-023", "Typed parameters"],
        ["DR-024", "Speech, image providers"],
        ["DR-025", "Image provider, cached"],
        ["DR-026", "Retries and profiles"],
      ]],
    ];
    cols.forEach(([m, items], i) => {
      const x = 0.4 + i * 3.1, w = 2.95;
      box(s, x, 1.12, w, 3.75, C.fill);
      text(s, m, { x: x + 0.15, y: 1.22, w: w - 0.3, h: 0.24, size: S.cap, bold: true, color: C.blue, cs: 1.5 });
      const step = i === 1 ? 0.62 : i === 2 ? 0.36 : 0.45;
      items.forEach(([id, d], j) => {
        const yy = 1.55 + j * step;
        text(s, id, { x: x + 0.15, y: yy, w: 0.75, h: 0.4, size: S.small, bold: true, color: C.blueDk });
        text(s, d, { x: x + 0.9, y: yy, w: w - 0.98, h: step - 0.04, size: S.small });
      });
    });
    text(s, "**Why it matters:** each record says what we chose, what we measured, and what would change our mind, so a new teammate or reviewer can see the reason.",
      { x: 3.65, y: 3.1, w: 2.65, h: 1.6, size: S.small, color: C.ink2 });
    source(s, "Source: build plan §7, decision records DR-009 to DR-026, dated September to 24 November 2026 [1]. DR-001 to DR-008 predate the course.");
    notes(s, [
      "Speaking notes · Presenter TBD (part C) · about 40 seconds",
      "",
      "- When a design choice costs more than an hour to undo, we write a decision record: what we chose, why, what we measured, and what would change our mind. There are eighteen since September.",
      "- October's big one is DR-016: the guard checks the model's own confidence in its target instead of matching the player's words, because we measured the word check refusing good requests.",
      "- November's added framework features the games needed: the game reports outcomes, decision rounds for many agents, typed parameters, speech and images as two more kinds of provider, and retries and profiles.",
      "",
      "If asked 'Who decides a record?': The lead or the framework tech lead proposes it with the measurement; the team lead accepts it. Each one names its status and date.",
      "If asked about DR-001 to DR-008: those came with the build plan from the prototype, before the course started; DR-001, the in-process model, is still 'probable' until Phase 6b.",
    ]);
  }

  // 11 ── Design: the phase plan ─────────────────────────────────────────────
  {
    const s = pres.addSlide();
    header(s, "3 · DESIGN UPDATES · PLAN", "Phases: planned and done", "C");
    // timeline Sep 2026 – Apr 2027, x from 2.6 to 9.6
    const months = ["Sep", "Oct", "Nov", "Dec", "Jan", "Feb", "Mar", "Apr"];
    const x0 = 2.75, x1 = 9.55, y0 = 1.12, rh = 0.33;
    const mw = (x1 - x0) / 8;
    const X = (m, d) => x0 + (m + (d - 1) / 30) * mw; // m: months since Sep
    months.forEach((m, i) => {
      text(s, m, { x: x0 + i * mw, y: y0 - 0.02, w: mw, h: 0.22, size: S.cap, color: C.ink3, align: "center" });
      s.addShape(pres.shapes.LINE, { x: x0 + i * mw, y: y0 + 0.25, w: 0, h: rh * 9 + 0.05, line: { color: C.rule, width: 0.75 } });
    });
    // [label, startM, startD, endM, endD, state, note]
    const ph = [
      ["0 · Skeleton", 0, 3, 0, 6, "done", ""],
      ["1 · Core, actions, schema", 0, 6, 1, 11, "done", "Closed 11 Oct"],
      ["2 · Provider, queue, memory", 1, 11, 1, 25, "done", "Closed 25 Oct"],
      ["3 · Validation", 1, 25, 2, 3, "done", "Closed 3 Nov"],
      ["4 · Evaluation harness", 2, 3, 3, 7, "now", "Due 7 Dec"],
      ["5 · Editor tooling", 3, 7, 5, 22, "plan", ""],
      ["6 · Cloud providers", 5, 22, 6, 8, "plan", ""],
      ["6b · In-process model", 6, 8, 6, 29, "plan", ""],
      ["7, 8 · Samples, demos", 6, 29, 7, 26, "plan", ""],
    ];
    ph.forEach(([l, sm, sd, em, ed, st, note], i) => {
      const y = y0 + 0.3 + i * rh;
      text(s, l, { x: 0.4, y, w: 2.3, h: rh - 0.06, size: S.small, valign: "middle", bold: st === "now" });
      const xa = X(sm, sd), xb = Math.max(X(em, ed), xa + 0.08);
      const fill = st === "done" ? C.blue : st === "now" ? C.blueLt : C.white;
      s.addShape(pres.shapes.RECTANGLE, { x: xa, y: y + 0.05, w: xb - xa, h: rh - 0.16, fill: { color: fill },
        line: { color: st === "plan" ? C.grey : C.blue, width: 1.25, dashType: st === "plan" ? "dash" : "solid" } });
      // labels go left of the bar where the "today" line would cross them
      const left = i === 2 || i === 3;
      if (note) text(s, note, { x: left ? xa - 1.25 : xb + 0.06, y, w: 1.19, h: rh - 0.06, size: S.cap, color: st === "now" ? C.blueDk : C.ink3, valign: "middle", bold: st === "now", align: left ? "right" : "left" });
    });
    // today
    const xt = X(2, 24);
    s.addShape(pres.shapes.LINE, { x: xt, y: y0 + 0.22, w: 0, h: rh * 9 + 0.1, line: { color: C.amber, width: 1.75, dashType: "dash" } });
    text(s, "Today, 24 Nov", { x: xt + 0.06, y: y0 + 0.3, w: 1.2, h: rh - 0.06, size: S.cap, color: C.amber, bold: true, valign: "middle" });
    text(s, "Phase 1 closed six days after its due date; Phase 2's date moved from 19 to 29 October and it closed on the 25th; Phase 3 closed six days early. Demo games ran beside every phase, as planned.",
      { x: 0.4, y: 4.42, w: 9.25, h: 0.5, size: S.small, color: C.ink2 });
    source(s, "Figure 3. Phase plan, our drawing from build plan §3 and the GitHub milestones' due and close dates [1]; COMP 491 covers phases 5 to 8.");
    notes(s, [
      "Speaking notes · Presenter TBD (part C) · about 35 seconds",
      "",
      "- The plan is a set of phases, each with a gate of tests. Solid blue is closed, light blue is now, dashed is next semester.",
      "- Phase 4, the evaluation harness, is due on 7 December, the first day of presentation week. Its A/B run is done; a second annotator's blind labels are what's left.",
      "- Editor tooling, other providers, samples and polished demos are COMP 491.",
      "",
      "HAND-OFF: \"<next presenter> will show you what all of this looks like in a game.\"",
      "",
      "If asked 'Why the evaluation harness before editor tools?': Every accuracy claim after it depends on it. That's decision DR-006, and the rule we won't cut.",
    ]);
  }

  // 12 ── Demo: what to watch for ────────────────────────────────────────────
  {
    const s = pres.addSlide();
    header(s, "4 · DEMO · PAINTED REALM", "What to watch for in the demo", "D");
    s.addImage({ path: FIG("painted-realm-council.jpg"), x: 0.4, y: 1.15, w: 5.3, h: 5.3 * 1080 / 1920, altText: "Painted Realm: the map of five realms and the player's council panel" });
    text(s, "Figure 4. Painted Realm, our strategy demo, at turn 1 (17 Nov 2026 build). Map painting generated with OpenAI's image model.",
      { x: 0.4, y: 4.18, w: 5.3, h: 0.5, size: S.cap, italic: true, color: C.ink3 });
    text(s, "ALL OF IT RUNS ON AGENERELA", { x: 5.95, y: 1.15, w: 3.7, h: 0.22, size: S.cap, bold: true, color: C.ink3, cs: 1 });
    bullets(s, [
      "**Rulers:** each country is an agent with no body",
      "**Personas:** each ruler's own character",
      "**Storyteller:** events the model invents",
      "**Advisor:** advice for the player's own turn",
      "**Guards:** an illegal order never reaches the game",
      "**Memory, observations:** what a ruler recalls and sees",
      "**Decision inspector:** every prompt and answer",
    ], { x: 5.95, y: 1.45, w: 3.7, h: 3.3, size: S.small, gap: 5 });
    tag(s, "work", 8.7, 0.66, 0.95);
    source(s, "Placeholder: the list follows the video once it is cut. Painted Realm is Demos/GreyBoxStrategy (#20); art terms in docs/demos/grey-box-strategy-screen.md.");
    notes(s, [
      "Speaking notes · Presenter TBD (part D) · about 35 seconds",
      "",
      "- This is Painted Realm, our strategy game. Five realms; four are ruled by a model, and you play the fifth.",
      "- Watch for the framework, not the game: every ruler is an Agenerela agent with no body in the scene. Each has a persona. A storyteller invents events, an advisor helps you, the guards stop illegal orders, and the decision inspector shows every prompt and answer.",
      "- The video is a replay of a recorded game, so you won't wait for the model.",
      "",
      "TEAM NOTE: placeholder. Update the bullets to match what the final video shows, in its order.",
    ]);
  }

  // 13 ── Demo: the video ────────────────────────────────────────────────────
  {
    const s = pres.addSlide();
    header(s, "4 · DEMO · VIDEO", "Demo: Painted Realm", "D");
    box(s, 0.4, 1.12, 9.25, 3.85, C.dark, null, null, 0.03);
    await iconDot(s, 4.65, 2.2, 0.75, ic.play);
    text(s, "VIDEO PLACEHOLDER", { x: 0.6, y: 3.1, w: 8.85, h: 0.35, size: 20, bold: true, color: C.white, align: "center" });
    text(s, "Recorded replay of a Painted Realm game, about 5:00, no model waits. Insert the video here in Google Slides (Insert → Video).",
      { x: 1.2, y: 3.5, w: 7.65, h: 0.6, size: S.body, color: C.onDark, align: "center" });
    source(s, "Video: our own recording of Painted Realm (Demos/GreyBoxStrategy), built by the demo session. Not yet inserted.");
    notes(s, [
      "Speaking notes · Presenter TBD (part D) · the video runs about 5 minutes",
      "",
      "- Start the video. Speak only if it has no voice-over; the narration then follows its chapters.",
      "",
      "TEAM NOTE: placeholder. The demo video is being made by another session; insert it here and paste its chapter list and narration into these notes. Also export a backup .mp4 in case the classroom can't play it from Slides.",
    ]);
  }

  // 14 ── Demo: after the video ──────────────────────────────────────────────
  {
    const s = pres.addSlide();
    header(s, "4 · DEMO · WHERE IT STANDS", "What you saw, and what is still open", "D");
    box(s, 0.4, 1.15, 4.5, 2.75, C.fill);
    text(s, "WORKING TODAY", { x: 0.6, y: 1.28, w: 4.1, h: 0.22, size: S.cap, bold: true, color: C.green, cs: 1.2 });
    bullets(s, [
      "Three games on one framework: **GreyBoxVillage**, **Living Vale**, **Painted Realm**",
      "A country, a guard and a merchant through the same `Agent` API",
      "A local 2B or 4B model on an ordinary graphics card",
    ], { x: 0.6, y: 1.6, w: 4.1, h: 2.2, size: S.body, gap: 10 });
    box(s, 5.15, 1.15, 4.5, 2.75, C.fill);
    text(s, "STILL OPEN, AND THE PLAN", { x: 5.35, y: 1.28, w: 4.1, h: 0.22, size: S.cap, bold: true, color: C.ink3, cs: 1.2 });
    bullets(s, [
      "Phase 4's second annotator (#148): by 7 Dec",
      "Editor tooling, a model inside the shipped game: COMP 491",
      "Two planned demos not started: **GreyBox2D**, **CompanionRPG**",
    ], { x: 5.35, y: 1.6, w: 4.1, h: 2.2, size: S.body, gap: 10 });
    box(s, 0.4, 4.1, 9.25, 0.8, C.blueLt);
    text(s, "**Try it:** open `Demos/GreyBoxStrategy` in Unity 6000.3.23f1 with Ollama running. A player build is a Phase 8 deliverable.",
      { x: 0.6, y: 4.1, w: 8.9, h: 0.8, size: S.body, valign: "middle" });
    source(s, "Source: Demos/README.md; GitHub milestones Phase 4 to 8; build plan §3 [1]. Status 24 Nov 2026.");
    notes(s, [
      "Speaking notes · Presenter TBD (part D) · about 35 seconds",
      "",
      "- So: three games run on one framework today, and a country, a village guard and a merchant all use the same agent API.",
      "- What's open: Phase 4's second annotator by 7 December, then editor tooling and a model inside the shipped game next semester. Two smaller demos we planned haven't started; the plan cuts those first if time runs short.",
      "",
      "HAND-OFF: \"<next presenter> will show how we kept six people's work in one repository.\"",
      "",
      "If asked 'Can I play it?': Yes, from the repository: open Demos/GreyBoxStrategy in Unity 6000.3.23f1 with Ollama running. A player build is a Phase 8 deliverable.",
    ]);
  }

  // 15 ── GitHub: branches and merge rules ───────────────────────────────────
  {
    const s = pres.addSlide();
    header(s, "5 · GITHUB · BRANCHES", "Branches, and the rules for merging", "E");
    // lanes
    const lane = (y, label, sub, color) => {
      text(s, label, { x: 0.4, y: y - 0.16, w: 1.55, h: 0.3, size: S.body, bold: true, color });
      text(s, sub, { x: 0.4, y: y + 0.12, w: 1.55, h: 0.4, size: S.cap, color: C.ink3 });
      s.addShape(pres.shapes.LINE, { x: 2.0, y, w: 7.6, h: 0, line: { color, width: 3 } });
    };
    lane(1.45, "Feature", "One per issue", C.ink3);
    lane(2.45, "test", "Default, integration", C.blue);
    lane(3.45, "main", "Releases only", C.dark);
    // feature branches merging into test
    [2.3, 3.4, 4.5, 5.6, 6.7, 7.8].forEach((x) => {
      s.addShape(pres.shapes.OVAL, { x: x - 0.07, y: 1.38, w: 0.14, h: 0.14, fill: { color: C.ink3 }, line: { color: C.ink3 } });
      arrow(s, x, 1.55, x + 0.35, 2.36, C.ink3, 1.25);
    });
    text(s, "PR and a green check", { x: 8.15, y: 1.75, w: 1.5, h: 0.4, size: S.cap, color: C.ink3 });
    // releases test -> main
    const rel = [[3.05, "v0.2.0", "18 Oct"], [4.15, "v0.3.0", "25 Oct"], [5.25, "0.4.0-preview.1", "29 Oct"], [7.4, "preview.2", "16 Nov"]];
    rel.forEach(([x, t, d]) => {
      arrow(s, x, 2.52, x + 0.3, 3.36, C.dark, 1.5);
      s.addShape(pres.shapes.OVAL, { x: x + 0.22, y: 3.37, w: 0.16, h: 0.16, fill: { color: C.white }, line: { color: C.dark, width: 2 } });
      text(s, t, { x: x - 0.35, y: 3.62, w: 1.3, h: 0.22, size: S.cap, bold: true, align: "center" });
      text(s, d, { x: x - 0.35, y: 3.83, w: 1.3, h: 0.22, size: S.cap, color: C.ink3, align: "center" });
    });
    // rules
    box(s, 0.4, 4.15, 9.25, 0.85, C.fill);
    bullets(s, [
      "**294** branches have opened a pull request so far.  **test:** pull requests only, the **Hygiene checks** must pass, no force-push or delete.  **main:** only the repository owner pushes, through release PRs.",
      "Big demo work had its own integration branches: **69-living-vale** (19 PRs), **153-gbv-v2** (5), **gbv-lab** (2).",
    ], { x: 0.55, y: 4.2, w: 9.0, h: 0.8, size: S.small, gap: 3 });
    source(s, "Figure 5. Our branch model (own drawing). Source: GitHub branch protection and tags, 24 Nov 2026; AGENTS.md \"Working conventions\" [4]; docs/RELEASING.md.");
    notes(s, [
      "Speaking notes · Presenter TBD (part E) · about 50 seconds",
      "",
      "- Three kinds of branch. Every piece of work starts on a feature branch named after its issue, and every one comes back through a pull request.",
      "- 'test' is our default branch, where everything integrates. To merge into it, a pull request has to pass our Hygiene checks; nobody can force-push to it or delete it.",
      "- 'main' is protected: only the repository owner can push, and only through a release pull request. Each release is a tag on main, one per closed phase: 0.2.0 after Phase 1, 0.3.0 after Phase 2, and two previews since.",
      "- Big demo games had their own integration branch for a while, like Living Vale with 19 pull requests, merged into test once stable.",
      "",
      "If asked 'How many branches?': 294 distinct pull request branches so far; 143 still on GitHub today.",
      "If asked 'Why not require reviews on test?': We agreed review before merge but didn't make GitHub enforce it; part F covers what that cost us.",
    ]);
  }

  // 16 ── GitHub: the work in numbers ────────────────────────────────────────
  {
    const s = pres.addSlide();
    header(s, "5 · GITHUB · THE WORK IN NUMBERS", "Pull requests and commits", "E");
    const stats = [["299", "Pull requests merged"], ["878", "Commits on test, merges excluded"], ["92", "Issues, 54 closed as done"], ["4", "Release tags on main"]];
    stats.forEach(([n, l], i) => {
      const y = 1.15 + i * 0.92;
      box(s, 0.4, y, 3.1, 0.8, C.fill);
      text(s, n, { x: 0.55, y, w: 1.15, h: 0.8, size: 26, bold: true, color: C.blue, valign: "middle" });
      text(s, l, { x: 1.65, y, w: 1.8, h: 0.8, size: S.small, valign: "middle" });
    });
    s.addChart(pres.charts.BAR, [{ name: "Merged pull requests", labels: ["Sep", "Oct", "Nov (to 24th)"], values: [28, 46, 225] }], {
      x: 3.75, y: 1.1, w: 3.3, h: 3.75, barDir: "col", chartColors: [C.blue],
      showValue: true, dataLabelFontSize: 12, dataLabelFontFace: F.sans, dataLabelColor: C.ink, dataLabelPosition: "outEnd",
      catAxisLabelFontSize: 12, catAxisLabelFontFace: F.sans, valAxisHidden: true, valGridLine: { style: "none" },
      showTitle: true, title: "Merged pull requests a month", titleFontSize: 12, titleFontFace: F.sans, titleColor: C.ink,
    });
    text(s, "MERGED INTO", { x: 7.3, y: 1.15, w: 2.3, h: 0.22, size: S.cap, bold: true, color: C.ink3, cs: 1 });
    table(s, ["Base", "PRs"], [["test", "267"], ["demo branches", "26"], ["main", "6"]],
      { x: 7.3, y: 1.42, w: 2.35, colW: [1.6, 0.75], rowH: 0.36, size: S.table });
    text(s, "Into main: **#95** (phases 0 and 1), then releases **#100, #114, #132, #339**.",
      { x: 7.3, y: 3.0, w: 2.35, h: 0.9, size: S.small, color: C.ink2 });
    source(s, "Source: GitHub API through gh, and git on origin/test, both 24 Nov 2026; first pull request 3 Sep 2026 (numbers.md).");
    notes(s, [
      "Speaking notes · Presenter TBD (part E) · about 25 seconds",
      "",
      "- In numbers: 299 merged pull requests, 878 commits on test not counting merges, 92 issues of which 54 are done, and four releases.",
      "- Most pull requests go into test; 26 went into the demo integration branches first; six went into main: the first set it up, the next brought phases 0 and 1, and four are our releases.",
      "",
      "If asked 'Why so many in November?': Smaller pull requests, one slice of a game at a time, and AI coding agents doing the mechanical work, each slice still a reviewed, tested pull request.",
    ]);
  }

  // 17 ── GitHub: problems and fixes ─────────────────────────────────────────
  {
    const s = pres.addSlide();
    header(s, "5 · GITHUB · PROBLEMS AND FIXES", "Git problems, and our fixes", "E");
    const rows = [
      ["Unity `.meta` files missing, or regenerated with new GUIDs, broke asset references", "**Hygiene checks** in CI and a pre-commit hook (#41, 21 Sep)", "Required on test and main; 17 checks today"],
      ["A merge with test **silently dropped** #90's memory tests and docs", "Restored in c9d6160 (24 Oct); diff against the branch's head after every merge", "A rule in AGENTS.md \"Lessons\""],
      ["`git reset --soft` onto a moved test would have **reverted** #342 and a UI slice", "Read both `git diff origin/test...HEAD` checks before every push", "Caught before merge, 17 Nov"],
      ["A commit pushed to #37 **after it merged** never reached test", "Check a PR is still open before pushing to it", "The commit landed as #38"],
      ["A new test that **could not fail** (#61)", "Break the rule once and watch the test go red", "Fixed in #62; used on the Phase 1 gate"],
    ];
    table(s, ["Problem", "Fix", "Result"], rows,
      { x: 0.4, y: 1.12, w: 9.25, colW: [3.65, 3.4, 2.2], rowH: 0.66, size: S.table });
    source(s, "Source: AGENTS.md \"Lessons\" and \"Before opening a pull request\" [4]; tools/ci/hygiene.py; pull requests #37, #38, #41, #61, #62, #342 and commit c9d6160.");
    notes(s, [
      "Speaking notes · Presenter TBD (part E) · about 55 seconds",
      "",
      "- Five problems, and what each taught us.",
      "- Unity's .meta files: if one is missing, or regenerated with a new ID, every reference to that asset breaks. So CI checks every pull request for it, and the same script runs as a pre-commit hook. Nothing merges with it red.",
      "- A merge with test once dropped a teammate's memory tests without any conflict. We restored them, and now diff the branch before and after every merge.",
      "- A soft reset onto a moved test would have quietly undone two merged pull requests. Two diff checks before every push caught it.",
      "- And a test that couldn't fail: now every new test has to be seen failing once.",
      "",
      "HAND-OFF: \"<next presenter> will cover how we managed the group.\"",
      "",
      "If asked 'What's in the 17 checks?': Missing or changed .meta files, broken JSON, generated files committed, files that skipped Git LFS, the wrong Unity version, the framework loaded from outside the repo, attribution trailers and the changelog.",
    ]);
  }

  // 18 ── Group management: tools ────────────────────────────────────────────
  {
    const s = pres.addSlide();
    header(s, "6 · GROUP MANAGEMENT · TOOLS", "How we tracked the work", "F");
    const cards = [
      ["GitHub Projects", "One board, \"Agenerela\": status, start and target dates, priority; the Roadmap and Insights views we submitted on 20 Oct"],
      ["Issues and milestones", "92 issues, which name the files their owner touches; one milestone a phase, ten in all; labels ready, blocked, research"],
      ["Gates and records", "A gate issue per phase lists the tests it must pass; decision records for design; findings.md for every measurement"],
      ["Shared rules", "AGENTS.md: one rules file for every teammate and every AI coding agent; REVIEWING.md for reviews"],
    ];
    cards.forEach(([h, b], i) => {
      const col = i % 2, row = Math.floor(i / 2);
      const x = 0.4 + col * 4.68, y = 1.15 + row * 1.6;
      box(s, x, y, 4.57, 1.45, C.fill);
      text(s, h, { x: x + 0.18, y: y + 0.12, w: 4.2, h: 0.3, size: S.head, bold: true });
      text(s, b, { x: x + 0.18, y: y + 0.48, w: 4.2, h: 0.9, size: S.small, color: C.ink2 });
    });
    text(s, "We did not use Jira: issues, the board and pull requests live in one place, linked to the code.",
      { x: 0.4, y: 4.4, w: 9.25, h: 0.35, size: S.small, color: C.ink2 });
    source(s, "Source: GitHub project \"Agenerela\" and milestones, 24 Nov 2026; course submission of 20 Oct (docs/course/2026-10-20-github-roadmap); AGENTS.md [4].");
    notes(s, [
      "Speaking notes · Presenter TBD (part F) · about 40 seconds",
      "",
      "- We ran everything on GitHub, not Jira, so the plan sits next to the code.",
      "- One project board with dates and priorities; you saw its roadmap in our October submission.",
      "- Every task is an issue that names the files its owner touches, so two people rarely edit the same file. Each phase is a milestone, and its gate issue lists the tests it must pass.",
      "- Design decisions go in decision records, measurements in our findings page, and one rules file, AGENTS.md, is read by every teammate and every AI coding agent we use.",
      "",
      "If asked 'Why no Jira?': A second tool means links between two systems. GitHub's board, milestones and issues did what we needed, and a pull request closes its issue.",
    ]);
  }

  // 19 ── Group management: contribution ─────────────────────────────────────
  {
    const s = pres.addSlide();
    header(s, "6 · GROUP MANAGEMENT · CONTRIBUTION", "Who did what, from GitHub", "F");
    const who = [
      ["Yevhen Mishchenko", "Draganoider", 827, 274, 26, 15],
      ["Hero Jaiyen", "phupanawat", 16, 9, 10, 0],
      ["Maxim Goloubitsky", "MaximGolubitsky", 21, 6, 5, 0],
      ["Timothy Brustinov", "TimBrustinov", 7, 5, 5, 0],
      ["Tigran Kolsuzyan", "JaySlifer", 4, 4, 3, 0],
      ["Hunter Hudson", "Hunaveli", 2, 1, 2, 0],
    ];
    table(s, ["Member", "GitHub", "Commits", "PRs", "Issues", "Reviews"],
      who.map(([n, g, c, p, i, r]) => [`**${n}**`, g, c, p, i, r]),
      { x: 0.4, y: 1.12, w: 5.7, colW: [1.6, 1.4, 0.86, 0.45, 0.62, 0.77], rowH: 0.32, size: S.cap, margin: [3, 4, 3, 4] });
    text(s, "Commits: non-merge commits on test by git author, matched to logins by e-mail. PRs: merged, by author. Issues: closed as done, by assignee. Reviews: formal GitHub reviews of someone else's PR.",
      { x: 0.4, y: 3.52, w: 5.7, h: 0.7, size: S.cap, color: C.ink2 });
    box(s, 0.4, 4.32, 5.7, 0.68, C.blueLt);
    text(s, "**AI-assisted work** counts toward the member who ran the agent: our rules forbid attribution trailers, so git cannot separate it. Many of the lead's commits were written by AI coding agents he directed.",
      { x: 0.52, y: 4.32, w: 5.5, h: 0.68, size: S.cap, color: C.ink, valign: "middle" });
    const pieColors = ["1F5FD1", "1E7B45", "8A5A00", "7A3FB0", "B23A3A", "4A4F57"];
    const names = ["Yevhen", "Hero", "Maxim", "Tim", "Tigran", "Hunter"];
    s.addChart(pres.charts.DOUGHNUT, [{ name: "PRs merged", labels: names, values: who.map((w) => w[3]) }], {
      x: 6.25, y: 1.05, w: 3.45, h: 1.95, chartColors: pieColors, holeSize: 50, showLegend: true, legendPos: "r", legendFontSize: 10, legendFontFace: F.sans,
      showValue: false, showPercent: false, dataLabelColor: C.white, showTitle: true, title: "PRs merged (299)", titleFontSize: 11, titleFontFace: F.sans,
    });
    s.addChart(pres.charts.DOUGHNUT, [{ name: "Issues done", labels: names, values: who.map((w) => w[4]) }], {
      x: 6.25, y: 3.05, w: 3.45, h: 1.95, chartColors: pieColors, holeSize: 50, showLegend: true, legendPos: "r", legendFontSize: 10, legendFontFace: F.sans,
      showValue: false, showPercent: false, showTitle: true, title: "Issues done, by assignee (51 of 54)", titleFontSize: 11, titleFontFace: F.sans,
    });
    source(s, "Source: git log origin/test and the GitHub API through gh, 24 Nov 2026 (numbers.md). One commit by \"Claude\" is not in the table; 3 done issues had no assignee.");
    notes(s, [
      "Speaking notes · Presenter TBD (part F) · about 45 seconds",
      "",
      "- Here is the work as GitHub records it, per member: commits, merged pull requests, issues done, and formal reviews.",
      "- The numbers are lopsided, and we want to be honest about why. The lead runs AI coding agents, and their commits go under his name, so his column is his work plus his agents'. We don't add attribution lines, so git can't split them.",
      "- The rest of us built the pieces in our own areas: targets and memory, telemetry and the prompt set, actions and profiles, availability and guards, and the core decision types. The next slide is how we tried to balance it.",
      "",
      "If asked 'Is this a fair picture?': Not entirely. Lines and commits don't measure labelling prompts, testing, research or design review; those don't show in git.",
      "If asked 'How did you use AI?': Claude Code and Codex, as coding agents and as a second reviewer. Every change still goes through a pull request and the same checks.",
    ]);
  }

  // 20 ── Group management: problems ─────────────────────────────────────────
  {
    const s = pres.addSlide();
    header(s, "6 · GROUP MANAGEMENT · PROBLEMS", "Group-work problems", "F");
    const rows = [
      ["Most of the core was built by one person (risk R01)", "From 27 Oct each member owns a **research area**: memory, targets, evaluation, observations, guards", "Areas own extension points; the core stays with the lead"],
      ["Review before merge was agreed, **not enforced** (R12)", "Review after merge, with a follow-up issue per defect: #61 → #62, #65 → #66", "11 of 299 merged PRs had a formal review"],
      ["Not everyone has a GPU that can run models (R14)", "Shared model servers: an RTX 5080, then a home RTX 4070 SUPER", "Anyone runs live tests from a laptop"],
      ["Exams and other courses (R04)", "Planned: unavailable dates posted each sprint, lighter sprints in exam weeks", "Not tracked in GitHub, so not measured"],
    ];
    table(s, ["Problem", "What we did", "Result"], rows,
      { x: 0.4, y: 1.12, w: 9.25, colW: [3.0, 3.85, 2.4], rowH: 0.72, size: S.table });
    source(s, "Source: risk management plan of 29 Oct 2026, risks R01, R04, R12, R14 (docs/course/2026-10-29-risk-management); build plan §6.6 [1]; GitHub review data (numbers.md).");
    notes(s, [
      "Speaking notes · Presenter TBD (part F) · about 55 seconds",
      "",
      "- Four problems from our risk register, and what came of them.",
      "- The biggest: most of the core was built by one person, which you saw in the numbers. From 27 October each of us owns a research area with its own extension point: memory, targets, evaluation, observations, guards.",
      "- We agreed to review before merging but never made GitHub enforce it, and only 11 of 299 merged pull requests had a formal review. So defects were found after merge; each got a follow-up issue, like 61 to 62.",
      "- Not everyone has a graphics card for models, so we set up shared model servers anyone can reach from a laptop.",
      "- What we'd do differently: turn on required reviews from day one.",
      "",
      "If asked 'Did the research areas work?': They gave each person a place to build without touching the core, and a clear owner for questions. Each area has one open research issue (#119 to #122, #138) where its pieces are linked.",
    ]);
  }

  // 21 ── Acknowledgement ────────────────────────────────────────────────────
  {
    const s = pres.addSlide();
    header(s, "ACKNOWLEDGEMENT", "Acknowledgement", "F");
    bullets(s, [
      "Our **COMP 490 instructor** and the **Department of Computer Science**, California State University, Northridge, for the course and their feedback",
      "The people we interviewed for our requirements in September",
      "The engine, tools and open models we build on: **Unity**, **Ollama**, the **Qwen3.5** models, **Newtonsoft JSON**, **LLMUnity**, **ComfyUI**",
      "The AI tools we used: **Claude Code** and **Codex** for code and review, **OpenAI's image model** and **ComfyUI** models for game art",
    ], { x: 0.4, y: 1.2, w: 9.25, h: 3.5, size: S.body, gap: 14 });
    source(s, "Tool and model licences are listed in Demos/README.md and tools/comfyui/README.md.");
    notes(s, [
      "Speaking notes · Presenter TBD (part F) · about 15 seconds",
      "",
      "- Thank you to our instructor and the department, to the people we interviewed, and to the open-source projects and AI tools this is built on. (Don't read the list.)",
      "",
      "HAND-OFF: \"Thank you. We're happy to take your questions.\"",
    ]);
  }

  // 22 ── References ─────────────────────────────────────────────────────────
  {
    const s = pres.addSlide();
    header(s, "REFERENCES", "References", "F");
    const refs = [
      "[1] Agenerela team, \"AI Agent Framework for Unity: Build Plan,\" docs/FRAMEWORK_BUILD_PLAN.md, GitHub: agenerela/agenerela, 2026.",
      "[2] Agenerela team, Software Requirements Specification and design figures, Sep. 2026, docs/course and docs/design.",
      "[3] Agenerela team, \"Findings,\" docs/llm-wiki/findings.md: every measurement, Oct.–Nov. 2026.",
      "[4] Agenerela team, AGENTS.md and REVIEWING.md, the repository's rules.",
      "[5] GitHub, agenerela/agenerela: pull requests, issues, milestones and branch protection, read with gh on 24 Nov. 2026.",
      "[6] Ollama. https://ollama.com (version 0.40.14 in our runs).",
      "[7] Qwen Team, Qwen3.5 model family. https://ollama.com/library/qwen3.5",
      "[8] undreamai, LLMUnity. https://github.com/undreamai/LLMUnity",
      "[9] Unity Technologies, Unity 6 (6000.3.23f1). https://unity.com",
      "[10] COMP490_L Final Presentation Template, course Google Slides, 2026.",
    ];
    const col = (list, x) => s.addText(list.map((r, i) => ({ text: r, options: { breakLine: i < list.length - 1 } })), {
      x, y: 1.15, w: 4.5, h: 3.85, fontFace: F.sans, fontSize: S.small, color: C.ink2, valign: "top", margin: 0,
      paraSpaceAfter: 10, isTextBox: true,
    });
    col(refs.slice(0, 5), 0.4);
    col(refs.slice(5), 5.15);
    source(s, "Every figure and chart in this deck is our own work, except the map painting in Figure 4 (generated with OpenAI's image model).");
    notes(s, ["Not presented; on screen while questions start, if needed."]);
  }

  // 23 ── Q&A ────────────────────────────────────────────────────────────────
  {
    const s = pres.addSlide();
    slideNo++;
    s.background = { color: C.dark };
    text(s, "Q & A", { x: 0.6, y: 0.5, w: 8.8, h: 0.25, size: S.kicker, bold: true, color: C.blueOnDark, cs: 2 });
    text(s, "Thank you! Questions?", { x: 0.6, y: 0.85, w: 8.8, h: 0.9, size: 40, bold: true, color: C.white });
    text(s, "The model chooses. Validated code acts.", { x: 0.6, y: 1.75, w: 8.8, h: 0.4, size: 18, color: C.onDark });
    const areas = [
      [TEAM[0], "Team lead · providers, the core"], [TEAM[1], "Agents · observations"], [TEAM[2], "Actions · evaluation"],
      [TEAM[3], "Availability · guards"], [TEAM[4], "Targets · memory"], [TEAM[5], "Telemetry · targets"],
    ];
    areas.forEach(([n, f], i) => {
      const x = 0.6 + (i % 3) * 2.95, y = 2.75 + Math.floor(i / 3) * 0.9;
      text(s, n, { x, y, w: 2.8, h: 0.3, size: 15, bold: true, color: C.white });
      text(s, f, { x, y: y + 0.32, w: 2.8, h: 0.3, size: S.small, color: C.onDark });
    });
    text(s, "github.com/agenerela/agenerela", { x: 0.6, y: 4.95, w: 8.8, h: 0.25, size: S.small, color: C.onDark2 });
    notes(s, [
      "EVERYONE · about 3 minutes of questions. Whoever owns the topic answers.",
      "",
      "Likely questions and short answers:",
      "- Why a small local model? It fits beside a game on an 8 GB card, and a shipped game can't pay per decision. The provider interface takes a bigger or cloud model with no framework change.",
      "- How fast is a decision? 0.23 to 0.56 s warm on the Phase 2 gate (qwen3.5:2b, RTX 5080); median 0.50 s on the Phase 4 A/B (home server).",
      "- How accurate is it? 66% on our 209-prompt set with qwen3.5:2b, guards on. qwen3.5:4b scored 82.8% on the same set, +16.7 points, beyond the 4.9 floor (findings, 23 Nov). The set is deliberately hard: impossible requests, situations, multi-turn.",
      "- Do the guards help? Not on accuracy beyond noise (66.0% vs 67.9%, floor 4.9 at n = 209). They turn wrong actions into doing nothing: 14 stopped, 4 right ones lost.",
      "- What if the model ignores the schema? Providers are untrusted: guards check every answer and Execute checks again before any handler runs.",
      "- Is this a chatbot framework? No: most calls come from the game itself, like turns, events and timers. A player's line is one kind of input.",
      "- Why so many commits by one person? The lead ran AI coding agents under his name; see slide 19. Teammates own research areas.",
      "- What's next? Phase 4's second annotator by 7 Dec; COMP 491: editor tools, cloud and in-process providers, samples, polished demos.",
    ]);
  }

  await pres.writeFile({ fileName: OUT });
  console.log("wrote " + OUT + " (" + slideNo + " slides)");
}

build().catch((e) => { console.error(e); process.exit(1); });
