# What each screen shows

Plain-English notes on the seven figures in this folder — see [`README.md`](README.md) for
what they are and how to re-render them. Written as the text we posted with each figure for
the COMP 490 Section 04 sketch/mockup practice, so it reads as prose, not as a specification.

---

## Team lead post — sitemap and screen flow

**Image:** `png/00-sitemap.png`

Agenerela is a Unity framework that lets a language model drive game agents, so it has two
kinds of users, and the sitemap has two areas.

On the left are the Unity Editor screens a **game developer** uses to build an AI agent, in
the order they are used: **(1) Agent Profile & Actions** to describe the agent, **(2) Agent
Behaviour** to put it in a scene and connect it to a model, **(3) Decision Log** to see why
it decided what it did, and **(4) Evaluation Report** to measure how often it is right. The
dashed arrow back to screen 1 is the loop: what the report shows is what the developer fixes
in the assets.

On the right are the games **players and our review panel** see. Each demo is its own Unity
project with its own start screen: **(5) GreyBoxVillage**, where the player talks to a guard
NPC, and **(6) GreyBoxStrategy**, where four countries each decide once per turn. CompanionRPG
and GreyBox2D come in later phases and are not designed in this lab.

The band along the bottom is the flow every screen is built around: the game asks, the
framework builds a menu of the actions that are legal right now, the model picks one from that
menu, guards re-check the choice, our own game code carries it out, and the telemetry is saved.
Each step is labelled with the screens where you can see it happening.

---

## Screen 1 — Agent Profile & Actions

**Image:** `png/01-agent-profile.png`
**Feature / screen:** describing an agent — the Unity Inspector for an Agent Profile asset.

This is the first screen a developer meets. They right-click in the Project window, choose
**Create ▸ Agenerela ▸ Agent Profile**, and fill in the Inspector. The top section says who the
agent is: name, role, personality and goals. The framework writes the prompt from these fields,
so the developer never writes one by hand. Below that is the list of actions this agent can ever
take, dragged in as Action assets. Each row expands so its example and its sensible targets can
be edited in place, with a preview of the example the model will be shown. The `none` action is
added by the framework, always last and locked, so nobody can forget it or delete it. A length
check keeps the descriptions comparable, because one long description measurably pushes small
models toward that option. Problems — an empty name, an empty row, a repeated id — appear right
here instead of only in the Console. One profile is shared by every agent that uses it: ten
village guards, one asset.

**What the numbers on the image point to:** ① identity fields · ② the action list · ③ the
description length check · ④ editing an action in place · ⑤ the built-in `none` · ⑥ validation
messages with a fix button.

---

## Screen 2 — Agent Behaviour

**Image:** `png/02-agent-behaviour.png`
**Feature / screen:** putting an agent into a scene — the Inspector for the Agent Behaviour
component.

The developer adds this component to a GameObject, here a village guard, and it links four
things together. It points at the shared Agent Profile from screen 1. It chooses where the model
runs — Ollama on the developer's machine while building, a cloud API or a model inside the game
later — and shows whether that is connected. It binds every action to a handler, which is the
developer's own C# code; an action with no handler is flagged, because it will never be offered
to the model. And it maps each target id to an object in the scene, which is the entire list of
things this agent is allowed to refer to.

The two panels at the bottom only come alive in Play mode. One shows exactly what the model is
being offered at that moment, which is how the framework's main rule becomes visible: illegal
options are missing from the list rather than forbidden in the prompt. The other lets the
developer type a stimulus, press **Decide**, and see the action, target, reply and latency
without writing any test code.

**What the numbers on the image point to:** ① the profile · ② the model provider · ③ actions
bound to game code · ④ targets in the scene · ⑤ what the model is offered right now · ⑥ try a
decision.

> **Since the lab (20 September).** Three of these callouts changed after we posted this.
> ② The model is configured in a provider config asset, and this field only overrides it;
> blank means the project default (DR-013). ③ Only asset-authored actions are bound here —
> an action declared in code is its own handler (DR-011). ④ Targets are discovered each
> decision from `Targetable` components near the agent, not typed per agent (DR-014). The
> text above is what we posted and stays as it was; the figure marks each change in its
> notes column, and figure 6 of [`workflow.html`](workflow.html) is the current picture.

---

## Screen 3 — Decision Log

**Image:** `png/03-decision-log.png`
**Feature / screen:** debugging decisions — an Editor window that answers "why did my agent do
that?"

Every decision made while the game runs appears here as a row: the time, the agent, what it was
asked, what it chose and how long it took. The icons separate four outcomes that look identical
in the game but need completely different fixes: the action ran, the model chose to do nothing,
a safety guard blocked the choice, or the request failed. "The guard just stood there" is the
single most common thing a developer has to explain, and this is where the answer lives.

Selecting a row opens the whole story on the right: the facts the agent was given, the exact
list of options the model was offered, its raw answer, each guard's verdict in order with its
reason, and the telemetry. The example shown is our safety claim in one row. The player said
"Attack Godzilla", which is not a registered target; the model tried to attack the training
dummy instead; the grounding guard noticed the dummy was never mentioned and rewrote the answer
to `none`. Nothing was attacked, and the log says exactly why.

**What the numbers on the image point to:** ① the live list · ② filters · ③ outcome icons ·
④ asked vs. answered · ⑤ the guard pipeline · ⑥ telemetry. *(The data shown is made up for the
mockup.)*

---

## Screen 4 — Evaluation Report

**Image:** `png/04-evaluation-report.png`
**Feature / screen:** measuring accuracy — the Editor window that runs our labelled prompt set.

This is how we prove the framework works instead of asserting it. The developer picks a labelled
prompt set — things a player says, situations an agent is asked about, events the game reports,
each with the answer it should give — a model, and two set-ups to compare, for example with the
safety guards off and on. Both arms run over the same prompts, with the same model, in the same
session, after a warm-up, with only one model loaded. Those conditions are on screen because our
own measurements drifted by ten points when they were not met.

The result is never one percentage. Every answer is sorted into correct, wrong but legal (the
agent visibly misbehaves), contained by a guard (the safety layer working), rejected when an
action was expected, or a pipeline error — because each of those needs a different fix. The
report also breaks accuracy down per category, so the headline number is never measured on
player chat alone, lists the prompts that were answered wrongly, and marks any difference too
small to trust at this number of prompts.

**What the numbers on the image point to:** ① the run set-up · ② five outcomes · ③ the noise
check · ④ accuracy per category · ⑤ the failure list · ⑥ labeller agreement and export.
*(All figures in the mockup are illustrative, not measured results.)*

---

## Screen 5 — GreyBoxVillage play screen

**Image:** `png/05-village.png`
**Feature / screen:** the NPC demo — what a player actually sees and does.

The player walks around a grey-box village. Within five metres of the guard, a "Press Enter to
talk" prompt appears and the player can type anything at all, for example "Go to the tower."
While the model decides, a "…" bubble sits over the guard for a second or two so the pause never
reads as a freeze. The guard then answers in a speech bubble and walks to the tower: our own
code moves it, the model only chose the action `move_to` and the target `tower`.

The tower, the bridge and the training dummy are the registered targets — the only things the
guard can be sent to. Houses and woods are scenery. If the player asks for something impossible,
like "Attack Godzilla", the guard answers in character and attacks nothing, which is the
framework's main safety promise seen from the player's side. An overlay on F1 shows the decision
behind every reply: action, target, latency and which checks ran. That overlay is what we put on
screen at the review demo.

**What the numbers on the image point to:** ① the world and its registered targets · ② the talk
radius · ③ the free-text box · ④ the thinking bubble · ⑤ the action running · ⑥ the F1 decision
overlay. The strip along the bottom storyboards one exchange and a refusal.

---

## Screen 6 — GreyBoxStrategy turn screen

**Image:** `png/06-strategy.png`
**Feature / screen:** the strategy demo — the proof that an agent does not have to be a
character.

Four countries share a map. None of them has a body, a position, pathfinding or a voice, and
each one is driven through exactly the same framework as the talking guard in screen 5. When the
player presses **End Turn**, the game asks each country for one decision in turn, and a progress
bar shows who is deciding.

The panel on the right is the selected country: its treasury, army, forts and relations, which
are plain game state our code would keep with or without any AI; the short sentences it is told
this turn ("At war with Orsk", "Harvest was poor"); and the moves it is allowed to make right
now. Moves that are illegal are not offered at all — Merrow's army is under fifty, so it is
never shown `declare_war`. Each decision lands in the event log as a proclamation, using the
same free-text field the village guard speaks out loud, and its action changes the map: an
envoy, a tax rise, a new fort. This is the demo our build plan says must never be cut, because
it is what makes Agenerela an agent framework rather than an NPC chat plugin.

**What the numbers on the image point to:** ① the turn bar · ② the map · ③ the country panel ·
④ what the country is told · ⑤ the moves it is offered · ⑥ the event log.
