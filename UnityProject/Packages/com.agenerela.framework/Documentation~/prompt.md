# The prompt — what the model reads, and the switches that change it

`PromptBuilder` assembles every request an agent sends: the system prompt, the few-shot
examples, the observations, the history and the stimulus. The schema travels with it. This
note is about one gap, the switches that close it, and two that shape the few-shot examples. The
action and target list is on by default since 24 November 2026, after an A/B run showed it helps
(DR-027). The code-built agent's idle example and the example-target fallback are off. A
counter-example, an example's statement and a target's `INamedTarget` name are shown as soon as a
developer writes one, which is their opt-in, and each has a per-call switch that leaves it out.
None becomes a default until an A/B run shows it helps (hard rule 2, #151).

**The gap.** A model served through Ollama never reads the schema. Ollama uses it only to
constrain sampling (findings, 10 October 2026), so the action list, the descriptions and the
target ids in it reach no local model. The system prompt says "Choose exactly one action from
the allowed list", and without the list below it never shows one: a local model would learn the
actions only from the few-shot examples, and the targets only from what the game writes into
observations. Living Vale and GreyBoxVillage both wrote every action and target into their
observations by hand (#190). So the list is on by default.

**An agent with no examples sends no example block.** When no action has an example, and there is
no idle example, counter-example or refusal to show, the request carries no few-shot block at all,
not its header `Examples of correct decisions:` alone, and no blank line where it would have been
(#244 item 3). Such an agent learns the actions only from the list below, or from what the game
writes.

## Listing the actions and targets

On by default. To send the prompt from before the list, as an A/B run's control arm does:

```csharp
var result = await agent.DecideAsync(line, new DecideOptions { ListActionsAndTargets = false });
```

A call that offers an action with a parameter sends this list whatever the option says, with the
parameter's values on the action's line: the list is the only place a local model can read them
([parameters.md](parameters.md)). So does a call with `Choices` (below).

The system prompt then gains a list, right after the instruction line:

```
Choose exactly one action from the allowed list. Use no_target when none of the listed targets applies.
Allowed actions:
- quote_purchase: Quote a price for something the customer wants to buy.
- offer_quest: Offer a quest.
- none: Take no action.
Targets:
- stock_mara_bread_one: One loaf of bread, 3 gold.
- north_tower
```

- **The actions** are exactly the ones the schema offers, in its order. An action whose
  availability check fails is not listed, and `none` comes last (build plan §2.2, rule 3). Each
  `Description` is put on one line and ends with a full stop. An action with no description is
  listed by its id alone. Keep descriptions to one short clause of similar length: a long,
  emphatic one biases a small model toward that action (Appendix A).
- **The targets** are the schema's, in its order, without `no_target`, which the instruction line
  already names. A target object can say what it is by implementing `IDescribedTarget`:

  ```csharp
  sealed class StockEntry : IDescribedTarget
  {
      public string Description => $"{Quantity} {Item}, {Price} gold.";
      // ...
  }
  shop.Add("stock_mara_bread_one", breadEntry);   // an ExplicitTargetSource
  ```

  Any other target is listed by its id. A scene target is registered as its `Transform`, so it is
  listed by id, and its `Targetable.Description` reaches the model as an observation through
  `ProximityObservationSource` instead (build plan §2.8). To tell the model the targets as
  observations without the list, add a `TargetSummarySource` to `Agent.ObservationSources`: one
  line per target category, each target by its id, its `IDescribedTarget` description in
  brackets, as in `place: bridge (the wooden bridge) and well (the stone well).` It calls no model
  and gives the same lines for the same targets; `Headings` words a category's line. Whether a
  small model reads it better than a line per target is #151's A/B (`EvalArm.SummariseTargets`).
- **The budget.** `MaxListedTargets`, 12 by default, caps the target lines. The first targets in
  schema order are kept, so a source that offers the nearest first, as `ProximityTargetSource`
  does, loses the far ones. **The cut is silent**: the prompt says nothing about targets left out,
  and the ones cut are always the last in schema order, registration order then each source's
  order, whatever matters most to the game (#244 item 5, N2). Order your sources and registrations
  so what the model must see comes first, or raise the cap. A target past the cap can still be chosen, but it gets no line. Every
  action is always listed: the developer decides how many there are, and dropping one would hide
  it. 0 lists no targets. A negative value throws at the call.
- **With the target rule.** On a provider without token probabilities, with `CheckTarget` on
  for the call, the target rule (`PromptOptions.TargetRule`, #107) comes after the list, as the
  last line.

The list's form follows the "Around you" list that DR-016's probe measured, which listed
targets the same way, but no actions. **Measured on its own** on the 209-prompt evaluation set
(findings, 23 November 2026, "ListActionsAndTargets off / on, again"): `qwen3.5:4b` went from 170
to 184 right, +6.7 points against a noise floor of 4.9, the same 418 answers on a repeat. Almost all
of the gain is in situations (+15.5), and wrong `none` answers fall from 14 to 3; it acted unasked
8 times against 4. `qwen3.5:2b` gained 4.8, within noise. It costs a line an action and a line a
listed target: the evaluation set's prompts grow from a median of 465 to 563 tokens, at most 546 to 663 (estimated, 3 characters
a token). DR-027 made it the default.

**When the cap cuts.** A target the cap leaves out is still offered by the schema, so nothing
fails, but a local model is told nothing of it. Each decision records the ids cut in
`DecisionTelemetry.TargetsUnlisted`, in schema order, and so does `AgentPreview.TargetsUnlisted` and
the decision log's `targetsUnlisted`. The request is the same with or without this record. Raise
`MaxListedTargets` when it is not empty and the far targets matter, as with a strategy game's 30
countries.

## Saying which targets each action may take

A game that declares target categories (`ActionDefinition.TargetCategories`), pairs its schema
(`PairTargetsWithActions`) or checks each target (`ITargetAvailability`, `[AvailableFor]`) has told
the framework which action may take which target. None of that reaches a local model: the list
above names every target, and the schema, which carries the pairs, is never read. So the model can
choose a pair the guards then refuse.

**To keep a model off illegal pairs, pair the schema** (`PairTargetsWithActions = true`). The
grammar then allows only legal pairs, and the prompt does not change. On the interim set, with
actions restricted to some targets, it brought the illegal pairs the model chose from 6 to 0 on
`qwen3.5:4b` and from 20 to 0 on `qwen3.5:2b`. It broke no answer and added no prompt tokens
(findings, 10 November 2026, "Saying which targets each action may take"). It does not raise the
score: 319 against 319, and 291 against 287.

Telling the model the pairs as well is an opt-in, and helps only a larger model:

```csharp
var result = await agent.DecideAsync(line, new DecideOptions { LegalTargets = LegalTargetListing.PerTarget });
```

`PerTarget` lists the actions and targets whether `ListActionsAndTargets` is on or not, and names,
on each target's line, the actions that may take it:

```
Allowed actions:
- follow_player: Walk behind the player. Target: no_target.
- move_to: Walk to a place.
- attack_target: Attack something.
- none: Take no action. Target: no_target.
Targets:
- bridge (for move_to)
- well (for move_to)
- docks (for move_to)
- wolf (for attack_target)
```

An action that may take no listed target says so on its own line: `Target: no_target.`, or
`Targets: none listed.` when `MaxListedTargets` cut its targets.

**What it measured.** On the interim set with restricted pairs, `qwen3.5:4b` scored 333 against
317, +4.1 points, beyond noise: impossible requests +10.9, events +9.2, calm scenes +15.
`qwen3.5:2b` lost 3.9 points with restricted pairs and 4.4 without, both beyond noise. It dropped
"stay here" commands to `none`, and walked to whatever place a calm scene named. On the curated 76
neither model moved. It costs about 50 to 140 more prompt tokens than the plain list. **Do not use
it with a 2B-class model.** Neither evaluation set restricts any pairs as it ships, and a number
from a real game is still owed. The other rendering tried, each action's line ending with its
targets, helped neither model, and was removed.

- **The pairs** are the paired schema's, made by the same rule from the same availability answer:
  an action takes the targets in its categories, or every target when it names none, less any its
  per-target check rejected for this decision, and `no_target` only when it does not require a
  target. So the list, the paired schema, `TargetCategoryGuard` and `TargetAvailabilityGuard` cannot
  disagree. With `PairTargetsWithActions` on too, the list reads the schema's own pairs.
- **What is left out.** An action that requires a target and may take none is not offered, in the
  schema and its examples either, as with pairing; `AgentPreview.LeftOut` says why. A target no
  offered action may take is not listed. `MaxListedTargets` counts only the targets that are, and
  `TargetsUnlisted` reports only those it cut.
- **The schema stays flat** unless `PairTargetsWithActions` is on. The list tells the model the
  pairs; pairing makes the grammar keep to them; the guards refuse a pair that breaks them either
  way.
- **Off**, the default, leaves the request byte for byte what it was before the option.

### As one line among the facts

`LegalTargetListing.ObservationLine` says the same pairs as one sentence among the observations,
grouped by action, instead of annotating the list:

```csharp
var result = await agent.DecideAsync(line, new DecideOptions { LegalTargets = LegalTargetListing.ObservationLine });
```

```
What you know right now:
- It is noon.
- Moves open to you: move_to bridge, well or docks; attack_target wolf.
- A cart has tipped over.
```

- **Where it goes:** after the lines of `Agent.ObservationSources` and before the call's own
  `DecideOptions.Observations`, so it reads as a standing fact. With
  `ObservationPlacement.WithStimulus` the call's own lines move after the stimulus and the sentence
  stays last in the block.
- **What it names:** each offered action that may take a listed target, by its id, in schema order,
  with those targets in schema order: `a, b or c`. Actions that take only `no_target` are left out,
  since the sentence names targets. With no action taking a target there is no sentence.
- **The same pairs as `PerTarget`,** from the same availability answer, with the same
  `MaxListedTargets` cap on the targets named and the same `TargetsUnlisted` report. An action that
  requires a target and may take none is not offered, as with `PerTarget`.
- **The system prompt is not changed.** Its list is `ListActionsAndTargets`' plain one, or none.
- **Rendered only.** The sentence is not in `AgentContext.Observations`: it takes no place under
  `Agent.MaxObservations`, and no guard, source or handler reads it.

It is a game's own moves line, made by the framework: GreyBoxStrategy's "Moves open to us: attack
fenmark or westmarch; ..." line made `qwen3.5:4b` attack 6 to 8 points more often than `PerTarget`'s
annotated list did (#247). On the interim evaluation set, though, the sentence scored below both the
plain list and `PerTarget`: beyond noise on `qwen3.5:2b` (−6.4 and −6.7 points), at the noise floor
on `qwen3.5:4b` (−3.6 and −3.3), with the 2B choosing to act where it should not (findings, "The
legal moves as an observation line", 11 November). It takes illegal pairs to none, but the paired
schema alone already does. Measure it on your own agent before using it.

## The idle example on an agent built in code

The few-shot block ends with an idle example, a line answered with `none`, so the model sees
that not every line asks for something. An agent built from an `AgentProfile` gets the
profile's `IdleExampleStimulus`. An agent built in code gets none, unless it is given one:

```csharp
var agent = new Agent(identity, actions, provider)
{
    IdleExampleStimulus = AgentProfile.DefaultIdleExampleStimulus,   // "Nothing has changed since last time."
};
```

The one used for a call is the first of these that is set: the call's
`DecideOptions.IdleExampleStimulus`, then the agent's `IdleExampleStimulus`, then the profile's.
An empty string at any level leaves the example out. `Agent.IdleExampleStimulus` is null by
default, so it changes nothing until someone sets it. Whether the idle example helps at all is
still unmeasured (build plan §2.3).

## Knowledge and statement rules

Two optional fields on `AgentIdentity`, so a profile carries them and a code-built agent gets
them through the identity it is given (#190, Living Vale #14):

```csharp
var identity = new AgentIdentity
{
    Name = "Mara", Role = "the miller", Personality = "Practical and plain-spoken.",
    Goals = "keep the mill running",
    Knowledge = { "The bridge closes at dusk.", "When a customer asks for flour you have, quote a price." },
    StatementRules = "One to three short sentences. Never claim to have done what you only talked about.",
};
```

The system prompt then reads, after the goals and before the instruction line:

```
Your goals: keep the mill running.
What you know:
- The bridge closes at dusk.
- When a customer asks for flour you have, quote a price.
Your statement: One to three short sentences. Never claim to have done what you only talked about.
Choose exactly one action from the allowed list. Use no_target when none of the listed targets applies.
```

Each knowledge line goes in as written, except that a blank line is skipped and a line's own line
breaks become spaces; the statement rules are put on one line and end with a full stop. Both
empty, the default, sends exactly the prompt sent before they existed. The headings are
constants in `PromptBuilder`.

**How the rules are introduced** is a per-call choice, `DecideOptions.StatementRulesWording`:

| Value | The line |
|---|---|
| `YourStatement` (the default) | `Your statement: <rules>` |
| `HowItShouldRead` | `How your statement should read: <rules>` |
| `InYourOwnWords` | `Write your statement in your own words, following these rules: <rules>` |

The default's heading names the field the model fills, so rules that open with a noun phrase ("A
trader's voice: profit, price or a deal.") can read as a statement already given. In GreyBoxStrategy
`qwen3.5:2b` returned its persona's whole rule as its statement 373 times in 582 (#249).
At that game's table `InYourOwnWords` took the 2B's copies of its rule from 15% of statements to
none, and `HowItShouldRead` to 7%. On the evaluation sets neither wording moved correct answers
beyond noise on `qwen3.5:2b` or `qwen3.5:4b`, but with `InYourOwnWords` the 2B repeated more
few-shot example lines as its statement (findings, "How a statement rule is introduced",
11 November).

**Which slot a line belongs in.** The model reads all three; what differs is how often a line
changes and who pays for it.

- **Knowledge** is what stays true for this agent: the game's rules for it ("When trouble is
  reported at a place you can reach, go there."), facts about its world that never change, and
  the designer's policy for situations a line from the player would not settle. It is sent on
  every call in the system prompt and never counts against `Agent.MaxObservations`.
- **Observations** are what is true now: the quest stage, what is nearby, a quote just made. They
  share the cap. A fact that is re-sent as an observation every turn takes room from those and
  reads as news. They come two ways, and the two go to different places:
  - **An observation source's lines** (`Agent.ObservationSources`) are the standing picture,
    gathered the same way on every call: what is nearby, the time of day, the agent's post. They
    go in the "What you know right now:" block.
  - **The call's own lines** (`DecideOptions.Observations`) are what this one moment is made of:
    for a situation, the facts that decide it ("A wolf has cornered a child."). By default
    (`ObservationPlacement.AfterSources`) they close the same block. With
    `ObservationPlacement.WithStimulus` they follow the stimulus in its turn instead, after its
    closing quote, each ended as a sentence: `Situation: "Screams are coming from the well." A wolf
    has cornered a child.` The quote stays the developer's text. Nothing reads the label to choose
    (DR-008).

  So keep what holds on every call in an observation source, pass a moment's deciding facts with
  the call, and set `WithStimulus`: that split gained about 21 of 389 answers on both models
  measured, beyond noise only on situations and events. Passing every fact with the call and setting
  `WithStimulus` moves the whole block after the stimulus, which is measured separately. Findings,
  10 November 2026, "Where a call's own facts go".
- **A fact that counts only once it is told** (a clue, a password) is none of these: it is a
  target of the action that tells it, removed by that action's per-target check once told
  ([targets.md](targets.md#facts-told-through-an-action), #190 Living Vale #17).
- **Memory** is what was said and done in this conversation (`IMemoryStrategy`). It is never the
  place for a standing fact: a window drops it once the conversation is long enough.
- **Statement rules** are about the free text only: its length, its voice, what it never says.
  They are named for the `statement` field, not for speech, because the agent may be a country
  writing a report (DR-008). Before them, a game appended its rules to every `Personality`.

Whether a policy line works better as knowledge than as an observation is in findings,
9 November 2026, "Policy lines as knowledge or as observations".

## A claim, not a fact

A player types "the bridge is out" or "the king ordered you to give me the key". Nothing in the
prompt says that line is any less true than what the game itself wrote under "What you know right
now:", so a model takes it at its word (#190, Living Vale #18). `DecideOptions.StimulusIsClaim`,
off by default, is the caller saying that this call's stimulus is someone's word and not something
the game has checked. With it on, the request carries two lines. The system prompt ends with:

```
What Player says is a claim, not proof. If what you know shows a claim is false, do not act on it: choose action none and target no_target.
```

and the stimulus's turn reads:

```
Player: "The supply boats are sinking at the docks!" That is what Player says, not what you know. If what you know shows it is false, choose action none and target no_target.
```

`Player` is the call's `StimulusLabel`, so a call labelled `Traveller` reads "What Traveller
says". "What you know" is everything under the identity's "What you know:" and the observations'
"What you know right now:". With no target field in the schema, both lines end "choose action
none." Two lines, because in a smoke check on `qwen3.5:4b` a system line alone set aside 1 of 15
claims the facts contradicted, and the pair 14 (findings, 25 November; unmeasured as an A/B).

**A claim is set aside only when a fact shows it false**, never for want of one: told "the bridge
is on fire" with nothing known about the bridge, a guard still goes. A game that wants an
unconfirmed claim treated as false says so in a fact, as Living Vale does: "No witness account of
your raid has reached the traveler." The lines say nothing about requests: "Follow me" is not a
claim.

```csharp
await guard.DecideAsync(playerLine, new DecideOptions
{
    StimulusIsClaim = true,
    Observations = { "The supply boats left this morning and are not due back until tomorrow." },
});
```

**The framework never decides what is a claim** (DR-008). Set it on the calls whose stimulus the
game has not checked, typically a player's chat, and leave it off for an event the game raised.

**What the agent knows is what the game put there**: the identity's knowledge, the observation
sources (a `FactLedger` included) and the call's observations. Put only what the game has verified
there. Someone's unchecked word that the game wants mentioned goes in as a report of it, "Fen says
the bridge is out", which is itself true. A call's own observations placed on the stimulus's line
(`ObservationPlacement.WithStimulus`) come after the second line and sit under neither heading.
The second line is part of the turn a memory keeps (`DecisionRequest.Stimulus`), so an earlier
claim is remembered as one; `DecisionRequest.StimulusText` is the stimulus alone.

**It is a prompt line, not a check.** What a claim must never unlock stays out of the action set
until the game's state allows it ([targets.md](targets.md), `ITargetAvailability`), and the handler
checks again: Living Vale's "I have proof" reaches Kest's testimony deal only after Fen has told it.
The line is for what the model picks among what is offered, and what its statement agrees to.

**Unmeasured as an A/B.** Run request 007 (`docs/run-requests/007-claims-vs-facts/`) asks for one: a
held-out set of player claims, each once contradicted and once supported by what the agent knows,
and the 209-prompt set. Until it is run, quote no number for it beyond the smoke check.

## Two texts in one statement: a title and a story

A decision has one free-text field. An event that wants a title and a story can ask for both in
it and split them (#266 item 3), as GreyBoxStrategy's storyteller does:

```csharp
identity.StatementRules = "A title of two to five words, a colon, then one or two sentences telling what happened.";

// In the handler, or wherever the game reads the statement:
string s = result.Decision.Statement ?? "";
int colon = s.IndexOf(':');
string title = colon > 0 && colon < 60 ? s.Substring(0, colon).Trim() : null;
string story = title != null ? s.Substring(colon + 1).Trim() : s.Trim();
if (title == null) title = gameMadeTitle;   // the model often leaves it out
```

- **Expect the title to be missing.** In GreyBoxStrategy's first storyteller, `qwen3.5:4b` left it out
  of 47% of events (findings, 12 November, "A storyteller in GreyBoxStrategy"). Have a fallback.
- **A second, short call is the measured fix.** A second agent asked only for the title, given the
  story, cut titles missing from 47% to 0%, at 0.35 to 0.39 s an event, 20 games an arm (findings,
  12 November, "The storyteller's stage S2"). It costs one more request per event; telemetry shows two
  unrelated decisions, since nothing links them (#266, N14).
- **Bound the split.** A colon inside the story is common; take the first one, and only when the
  part before it is short.

A second text field is not built: one game needs it so far.

## Counter-examples

A counter-example is a line that touches an action's subject but asks for nothing, shown
answered with `none`. Living Vale's shopkeepers quoted prices nobody had asked for. One line
answered with `none`, sent as the idle example, cut those unasked offers from 6/13 to 0/13 on
one small model and from 10/13 to 0/13 on another, and asked-for trades held. That was measured
in the game, on its branch `69-living-vale` (its findings, 5 November: "Unasked offers"), not
on the evaluation set. A counter-example is the same idea tied to one action, and is itself
unmeasured.

```csharp
quote.CounterExampleStimulus = "Any rumours about the wagon?";   // an ActionDefinition; on its asset, asset.Action
```

```csharp
[AgentAction("quote_purchase", "Quote a price for something the customer wants to buy.", RequiresTarget = true)]
[Example("How much for the {0}?", "bread")]
[CounterExample("That {0} smells good.")]
public void QuotePurchase(AgentContext ctx, StockEntry entry) { /* ... */ }
```

`FewShotBuilder` puts it right after the action's own example, and only while the action is
offered:

```
Player: "How much for the Bread?" -> action: quote_purchase, target: bread
Player: "That Bread smells good." -> action: none, target: no_target
```

A `{0}` is filled the way the example's is, with the first registered target in
`PreferredExampleTargets`. If none of them is registered, the counter-example is left out, so a
placeholder never reaches the model. The field is empty by default. Writing one is the opt-in,
and `DecideOptions.CounterExamples = false` leaves them all out of a call.

**Keep it apart from the evaluation set** (hard rule 4). A counter-example is a few-shot example
like any other. `PromptSetChecks` flags a prompt that matches one, so `EvalRunner` refuses to
run that set. A counter-example also carries a risk: it pushes the model toward `none`, and in
the prototype a prompt biased toward `none` scored below the baseline (Appendix A). So the A/B
has to check that real commands still score, as well as whether unasked actions go down.

## Examples that carry their situation

When the game asks the same thing every time ("Choose this turn's move."), the situation lives only
in the observations, and an example of a stimulus and an answer cannot show *when* to pick an
action (#216 item 1). An example can carry one or two observation lines,
`ActionDefinition.ExampleObservations`, or `[Example("...", Observations = new[] { "..." })]` in
code, shown above the example under the heading a real call's observations have:

```text
Examples of correct decisions:
What you know right now:
- Our treasury holds 3 gold.
Report: "Choose this turn's move." -> action: raise_taxes, target: no_target
Report: "Rest the army." -> action: rest, target: no_target
```

- **Off by default.** Only a call with `DecideOptions.ExampleObservations = true` shows them. Off,
  or with none written, the block is byte-identical to one built before. It changes the examples,
  so it is owed an A/B run (#151).
- One format: the same heading and `- ` lines as the call's own observations, one line each, so the
  model reads an example's situation as it reads its own. An example without observations stays
  one line. A `{0}` names the example's target, as in its stimulus.
- **Hard rule 4.** An example with a situation is its stimulus and its observations together, so
  `PromptSetChecks` flags an evaluation prompt whose stimulus matches the example *and* whose
  observations hold every line of its situation; the same stimulus in another situation is another
  prompt. An `EvalRunner` arm with the option off shows each example's bare stimulus, so for that
  arm the stimulus is judged alone, as before, and the run is refused if it matches.
- Only an action's own example carries one; counter-examples and the idle example do not.

## Statements in the examples

An example without a statement leaves the model one line to copy: its stimulus. GreyBoxVillage's
guard answered "Quiet night, Bram?" with "Fine day for it.", the stimulus of its idle example, in
4 of 4 live tries (findings, 5 November: "GreyBoxVillage's situations played live"). An example
can now show what the agent says too, after its target, in the reply's own field order:

```
Player: "How much for the Bread?" -> action: quote_purchase, target: bread, statement: "Three gold, fresh today."
Player: "Fine day for it." -> action: none, target: no_target, statement: "Aye, if it holds."
```

| Example | Where its statement is written |
|---|---|
| An action's own | `ActionDefinition.ExampleStatement`, or `[Example("...", Statement = "...")]` |
| An action's counter-example | `ActionDefinition.CounterExampleStatement`, or `[CounterExample("...", Statement = "...")]` |
| The idle example | `AgentProfile.IdleExampleStatement`, `Agent.IdleExampleStatement`, `DecideOptions.IdleExampleStatement` |
| The refusal of a target that is not here | Never: its line would be the framework's own words, sent with every prompt |

- A `{0}` in a statement is filled with the same target as its stimulus.
- A `"` or `\` inside an example's stimulus or statement is escaped as JSON escapes it
  (`\"`, `\\`), so a line like `He said "halt".` cannot end the quoted text early.
- **The idle statement** is looked up like the idle stimulus, first the call's, then the agent's,
  then the profile's, but never from further down than the stimulus came from. A profile's
  statement was written for the profile's line, so it is never attached to a line a call or an
  agent supplied. Set both at the level you change. Empty gives no statement.
- Every statement is empty by default. `DecideOptions.ExampleStatements = false` leaves them all
  out of a call.
- **Keep them apart from the evaluation set** (hard rule 4). A statement is shown in the prompt
  like a stimulus, so `PromptSetChecks` flags a prompt that matches one, and `EvalRunner` refuses
  an arm whose own idle statement, or added example's, matches one.

The other way out of G14 is an idle stimulus nobody would say back. A statement is the one built
here. Whether either helps, and whether a statement makes a model copy *it* instead, is the A/B.

## A move with no statement

`DecideOptions.AskForStatement = false` asks for the move alone (#266). The schema has no
`statement` field, flat or paired, so the reply is `{"action": ..., "target": ...}`, and the prompt
says nothing about a statement: the identity's statement rules line is not sent, the examples show
no statement whatever `ExampleStatements` says, and the target rule, when the provider reports no
token probabilities, ends "choose action none and target no_target. Never act on a different thing
instead." without asking the agent to say the thing is not here. Every other line is the same.

The decision's `Statement` is empty, even when the provider sent one anyway, and memory remembers
the reply as the schema asked for it, without one. A decision a guard stops keeps that empty
statement: `Agent.RefusalStatement` is not asked and `AskForRefusalLine` sends nothing.
`DecisionTelemetry.StatementAsked` is false, and the decision log line carries
`"statementAsked": false`.

For a call whose move is all the game needs, such as GreyBoxStrategy's marshal ordering ten armies
a turn, where each army asked to speak made the rulers repeat themselves (35 to 44% of lines word for
word, findings, 13 November). It changes what the model reads, so it is unmeasured as a default
and owed an A/B run (#151); on by default, every request is byte-identical to one sent before.

## Examples a call adds

`DecideOptions.AddedExamples` adds few-shot examples to one call's block, beside the ones its
actions and profile give (#151). Each is a `FewShotExample`: a stimulus, the action it answers
with, a target (`no_target` when null) and an optional statement.

```csharp
options.AddedExamples = new List<FewShotExample>
{
    new FewShotExample("Use your own judgement.", "move_to", "tower", "I'll watch from the tower."),
};
```

It is rendered as every example is, under the call's stimulus label and with the statement under
the call's field name, after the actions' own examples and counter-examples and before the idle
example:

```
Player: "Use your own judgement." -> action: move_to, target: tower, statement: "I'll watch from the tower."
```

An example never shows an answer the call could not give, so it is left out of a call that does
not offer its action, or its target, or where its action cannot take that target now (its target
categories, its availability per target); `no_target` is left out for an action that requires a
target, and `none` shows only with `no_target`. The statement is shown only when
`ExampleStatements` is on and the call asks for one. Its text is sent as written: a `{0}` is not
filled. Null or an empty list, the default, leaves the block byte-identical.

For an A/B run of one example in one arm, such as a free choice answered with an action, for a
model that answers `none` when the choice is left to it. Unmeasured, so never a default. The evaluation runner
refuses a run in which an arm's added example, or its statement, matches a prompt's stimulus
(hard rule 4).

## The free-text field's name

`DecideOptions.StatementFieldName` names the free-text field for one call: `statement`
(`DecisionSchema.StatementFieldName`) unless set, and null means the same. Set
`DecisionSchema.DialogueFieldName`, `dialogue`, to ask under the prototype's name, for the A/B
run of the rename (DR-008, #151). Only the name changes. The field still comes last, after
`action`, `target` and any parameters, so the reply is `{"action": ..., "target": ...,
"dialogue": ...}`, and the decision's `Statement` holds what the model wrote there.

The schema carries the name (`DecisionSchema.StatementName`), so everything that reads the field
reads it under the name that was sent: the parser, the field-order check
(`DecisionTelemetry.OrderLost`, #180), the JSON schema, the examples (`..., dialogue: "On my
way."`), the identity's statement rules line ("Your dialogue: "), memory, a multi-turn prompt's
staged history in the evaluation runner, and a refusal line's request (#109). A reply that writes
the other name has no statement: under `dialogue`, a `statement` key is ignored, as any key the
schema did not ask for is. Telemetry, the decision log and the evaluation report keep their own
key, `statement`, whatever the call named the field.

The name must be lowercase snake_case and none of `action`, `target`, `also_targets` and `outcome`
(the key a remembered outcome takes), or the call throws `ArgumentException`; an offered action
with a parameter of the same name fails the call as a `RequestPreparation` error before anything
is sent. A memory snapshot does not record the name, so a decision imported from one is read as if
its statement were `statement`. With the default name every request is byte-identical to one sent
before the option. The rename is unmeasured, so it is a choice per call, never a default, until the
run says otherwise.

## A choice among options given with the call

`DecideOptions.Choices` asks the agent to pick one of a list the call passes, instead of its
registered actions (DR-022, #266 item 2): a dilemma's two or three options, say, without
registering every option of every dilemma as an action and keeping the rest unavailable.

```csharp
var result = await ruler.DecideAsync("Mercenaries are at the gate, asking for work.", new DecideOptions
{
    Choices = new List<Choice>
    {
        new Choice("hire_them", "Pay 30 gold, and 14 soldiers join our army"),
        new Choice("send_them_away", "They leave, and nothing changes"),
        new Choice("seize_their_arms", "Take their weapons by force", Availability.No("no army at hand")),
    },
});
string picked = result.Decision.ActionId;   // "hire_them", "send_them_away" or "none"
```

- **The schema** offers the available choices in the order given, then `none`, and has no
  `target` field, since a choice takes none. The agent's own actions and targets are not offered.
- **The prompt** lists the offered choices with their descriptions ("Allowed actions:"), whatever
  `ListActionsAndTargets` says, because a model served through Ollama never reads the schema. No
  example of the agent's actions is shown; the idle example is. Identity, observations and memory
  are the agent's, as on any call.
- **An unavailable choice** (`Choice.Available`, an `Availability`) is left out of both, and its
  reason goes to `DecisionTelemetry.ChoicesUnavailable` and the log's `choicesUnavailable`; the
  offered ids go to `ChoicesOffered` and `choicesOffered`.
- **The guards** run as on any call: `SchemaLegalityGuard` rejects an id that was not offered, and
  a guard of your own reads the list from `input.Options.Choices`.
- **`Execute`** runs no handler and no re-check for a chosen option, records `Done` and settles
  memory held by `MemoryCommit.OnExecute`. The game acts on the id. A remembered choice is
  replayed to later calls like any decision; give the agent its own memory, or none, if it should
  not be.
- A bad list (empty, a null entry, an id an action could not have, `none`, an id twice, an empty
  description) throws at the call.

The prompt a choice call sends is new and unmeasured (#151). With `Choices` null, the default,
nothing changes.

## The example's target

An example that needs a target names the first registered one from its action's
`PreferredExampleTargets`, and is left out when none of them is here. It calls the target by a
name made from its id's words. A game whose targets change every turn (stock runs out, quests
move on) has to rewrite that list before every decision; Living Vale's `RefreshExamples` does. A
switch, off by default, and an interface change that (#190, Living Vale #20).

```csharp
await agent.DecideAsync(line, new DecideOptions { ExampleTargetFallback = true });
```

**The fallback.** With `ExampleTargetFallback` on, an example with no preferred target here names
the first offered target its action takes instead: any target, unless the action declares
`TargetCategories`, then the first in one of them. It skips a target the stimulus mentions, so the
example never shows the model the answer to the request it travels with. A word of the target's
id or name counts when the stimulus says it whole, at any length ("ox"); when it starts a word of
the stimulus, from three letters ("inn" in "innkeeper", "tower" in "towers"); or when a word of the
stimulus of four letters or more starts it ("dock" for `docks`). An id's words include an acronym's
(`NPCGuard` is "npc" and "guard"). "The", "a", "an", "of", "to", "and", "for", "with" and "from"
never count, unless a name holds nothing else, and then all of them must be said. With every
target the action takes mentioned, the example is left out, as before. A counter-example with a `{0}` follows its example.
A preferred target that is here is used as before, mentioned or not. It stays off: on the interim
set, whose targets have no categories, it showed "Strike the Bridge." as a correct attack, and
impossible requests fell about 30 points (findings, 9 November). Turn it on where targeted actions
declare `TargetCategories`.

**Names.** A target is called by its id's words, lowercase: `training_dummy` is "training dummy",
`village-gate` "village gate" and `NPCGuard` "npc guard", with a capital where it starts a
sentence ("North tower is quiet."). Until 9 November the words were run
together ("Engage the TrainingDummy."); the A/B found the two within noise on `qwen3.5:4b` and
`qwen3.5:2b`, and the spaced wording was adopted. The refusal of a target that is not here drops the article before `{0}`
where the target ends its phrase (at the end, before punctuation, or before a word such as "for",
"now" or "please"), so a move_to stimulus of "Go and stand by the {0}." gives "Go and stand by
Atlantis." rather than "the Atlantis". Where the phrase runs on past `{0}`, the article stays and a
common noun takes the place of the proper name, "an" as "a": "The sunken city is unstaffed.",
"Open the sunken city gate.", "Bring a sunken city-shaped token."

The refusal never names a place an offered target is called, by its id or
by the name an example gives it: a target named "Atlantis" makes it "Avalon" ("sunken city",
"lost city"), and with both taken there is no refusal.

**A target's own name.** A target is called by its `INamedTarget.Name` when it
implements one, as written:

```csharp
sealed class StockEntry : IDescribedTarget, INamedTarget
{
    public string Name => Item;                                  // "healing potion"
    public string Description => $"{Quantity} {Item}, {Price} gold.";
}
```

`INamedTarget` is separate from `IDescribedTarget`: the description is a sentence for the list
("One loaf of bread, 3 gold."), and would not fit inside "How much for the {0}?". No target has a
name until a game writes one, which is the opt-in.

## A budget for the prompt's size

A game's prompt grows with every feature it adds, and nothing says so. In Painted Realm the court's
prompt grew by 136 tokens a request when the economy went on, and 35% of its requests passed 1,600
tokens (findings, 23 November 2026). Nothing was cut: Ollama's context window
(`OllamaSettings.ContextSize`, `num_ctx`) is 4,096. The costs of a long prompt are latency and, on
a small model, perhaps worse choices. A **prompt budget** makes the size something the developer
sets and the telemetry sees (#151). There is none by default, and with none every request is what it
was before budgets existed, byte for byte.

```csharp
// Every call of this agent: measure against 1,600 tokens, and warn once if over.
court.PromptBudget = new PromptBudget(1600);

// Or allow it to leave things out, in this order, only while it is over.
court.PromptBudget = new PromptBudget(1600,
    PromptTrim.OptionalObservations, PromptTrim.UntakeableTargets, PromptTrim.History);

// One call: its own budget, or PromptBudget.None to turn the agent's off.
await court.DecideAsync(stimulus, new DecideOptions
{
    Observations = mustKnow,
    OptionalObservations = new List<string> { "Rival armies we see: ...", "Pikes beat riders, ..." }, // most worth keeping first
    PromptBudget = new PromptBudget(2048),
});
```

**How it is measured.** Before sending, with no model call, the request's text (system prompt,
examples, observations, every history entry and the stimulus turn, never the schema, which a local
model does not read) is counted in characters and divided by `CharsPerToken`, 3 unless the budget
says otherwise. On 6,304 of Painted Realm's requests to `qwen3.5:4b`, Ollama's own count,
`prompt_eval_count`, chat template included, came to 3.10 characters a token, and between 2.94 and
3.30 for every one (findings, 24 November 2026). The default errs long by 3.6% on average (53 of the
6,304 came out short, by 2% at most), so a trim fires a little early rather than late. After the reply,
the provider's own count decides whether the decision went over, when it reports one. For another
model or language, divide `PromptCharacters` by `PromptTokens` over a few decisions and pass your own
ratio: `new PromptBudget(1600, 3.4f)`. `RollingHistory`'s own token cap still divides by 4, which on
this model undercounts by about a fifth.

**What it records.** Every decision, budget or not: `DecisionTelemetry.PromptCharacters` and
`PromptTokensEstimated`. With a budget: `PromptBudget`, `OverBudget` (after every trim; on the
provider's count when it gave one) and `PromptTrims`, one entry for each trim that left something
out, such as `"History: 1 exchange"`. The decision feed and the Decision Log carry all five
([logging-decisions.md](logging-decisions.md)). The first decision of an agent that goes over its
budget logs one warning; later ones are only recorded.

**What it may leave out, and only that.** With no trims a budget only measures and warns. Each trim
listed is tried in the order given, one step at a time, and only while the estimate is over:

| Trim | One step | Recorded in |
|---|---|---|
| `OptionalObservations` | The last of the call's `DecideOptions.OptionalObservations` | `ObservationsDropped` |
| `UntakeableTargets` | Every target line of the action and target list that no offered action may take now, by its categories and per-target checks, at once. Only with `ListActionsAndTargets` on; with `LegalTargets` the list holds only those already | `TargetsUnlisted` |
| `History` | The oldest remembered exchange; memory keeps it | `HistoryTurnsDropped` |

`OptionalObservations` are sent as the call's own lines after its `Observations`, wherever
`ObservationPlacement` puts those, and count against `MaxObservations` the same way. A line the
trim leaves out is out of the context the guards and handlers read too. No trim ever removes an
action, a must-send observation or a target from the schema, and the field order is untouched:
the guards judge the same agent with a budget as without. When every listed trim is spent and the
prompt is still over, it is sent as it is, and recorded as over.

**Choosing one.** A budget with trims changes what the model reads when it fires, so it is a choice a
game measures before keeping (hard rule 2). Painted Realm's court is the first measured: see
findings for the run. A budget without trims costs nothing and changes nothing; it is the way to find
out how often a game's prompts run long.

## Measuring them

Each switch is an option an `EvalArm` carries, so one run in one session compares an arm with it
against an arm without it. `report.json` records each arm's `listActionsAndTargets`,
`maxListedTargets`, `counterExamples`, `idleExampleStimulus`, `idleExampleStatement`,
`exampleStatements`, `exampleTargetFallback`, `legalTargets` and `stimulusIsClaim`, and `pairTargetsWithActions`
for the paired schema ([targets.md](targets.md#the-paired-schema)), which changes the schema rather
than the prompt. Knowledge is the identity's rather than a call's, so an arm carries it as
`EvalArm.Knowledge`, lines added to the profile's for that arm's agents without changing the asset,
and `report.json` records it as each arm's `knowledge`. The evaluation set's targets declare no
categories, so legality cannot matter on it as it is; an arm can carry its own, `EvalArm.Legality`
(an `EvalLegality`: categories for the set's targets and the profile's actions, and extra targets),
applied to copies so neither the set nor the profile changes, and recorded as each arm's `legality`.
The runner refuses an arm whose legality makes a label's expected pair illegal.
[`evaluation.md`](evaluation.md#running-an-ab) explains how to run an A/B. Until one has been
run, quote no number for any of these on the evaluation set.
