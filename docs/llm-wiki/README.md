# LLM Wiki — Agenerela

Working knowledge base for whoever (human or AI) touches this repo next. It exists
because this project runs a measure-hypothesise-measure loop, and that loop produces
knowledge that is expensive to rediscover.

## Where things live

| Question | Where |
|---|---|
| What are we building, and in what order? | [`../FRAMEWORK_BUILD_PLAN.md`](../FRAMEWORK_BUILD_PLAN.md) — the plan of record |
| Why is the design the way it is? | Build plan §7, Decision Records |
| What was already measured in the prototype? | Build plan, **Appendix A** |
| What have *we* measured since? | [`findings.md`](findings.md) |
| Which developer guards should we explore, and how will we evaluate them? | [`guards-research.md`](guards-research.md) — source review and proposed work for [#138](https://github.com/agenerela/agenerela/issues/138), researched 4 November 2026, with the statement-replacing verdict added 14 November (#266 item 4); no new measurements |
| How do I set my machine up, or use another machine's Ollama over the network? | [`environment.md`](environment.md), including "Ollama on another machine, over the local network", which points to the RTX 5080's Docker server in [`tools/ollama-server/`](../../tools/ollama-server/README.md); a run another machine should do is a file in [`../run-requests/`](../run-requests/) |
| How does an agent check a running game, or run the EditMode tests, without the mouse? | [`environment.md`](environment.md#the-unity-cli-in-game-checks), "The Unity CLI: in-game checks" |
| What do I check before I write down an accuracy or latency number? | [`measuring.md`](measuring.md) — build plan §4's seven rules, and what has been learned since |
| Which other models might beat `qwen3.5:2b`, or `qwen3.5:4b` in about 4 GB, and which to A/B first, for 8 GB cards and for 12, 16 and 24 GB and up? | [`model-candidates.md`](model-candidates.md) — researched 4 November 2026, with a 4B-class section against `qwen3.5:4b` researched 11 November (its first two arms, Gemma 4 E4B and Nemotron 3.5 Bolt E4B, since run: `findings.md`, "Two 4B-class candidates against `qwen3.5:4b`"); it also notes fine-tuning the 2B as an idea, not planned; the scores are in `findings.md`, "Thirteen models on the evaluation set" and, on the interim set, "Thirteen models on the interim set" |
| Could a decision model (Jev, Gemini Decision 1, the open `tev1`, `d1` and others) or another engine that does not generate choose the action and target: embeddings, rerankers, classifiers, utility AI, planners, RL? | [`non-llm-deciders.md`](non-llm-deciders.md), researched and measured 12 November 2026: decision models first, with the question format measured on our 4B, then a survey of the rest and three embedding models, a keyword parser, cascades and a hybrid run against `qwen3.5:4b`; the numbers are also in `findings.md`, "Non-generative deciders" and "Decision models" |
| What can the decision schema hold today, and what must a serializer write for each shape? | [`decision-schema.md`](decision-schema.md) — the four shapes (flat, no target field, paired, statement only), any of the first three with no statement (#266), a choice call (DR-022), what `JsonSchemaSerializer` promises and refuses, and what that means for a GBNF grammar ([#126](https://github.com/agenerela/agenerela/issues/126)); read from the code on 12 November 2026 |
| What did the 8 October sword trials try, and what did they find? | [`asset-quality-2026-10-08.md`](asset-quality-2026-10-08.md), with its raw data in [`asset-quality-2026-10-08.json`](asset-quality-2026-10-08.json) |
| What changed, release by release? | [`../../CHANGELOG.md`](../../CHANGELOG.md) at the root for the repository, and the package's [`CHANGELOG.md`](../../UnityProject/Packages/com.agenerela.framework/CHANGELOG.md) for its API |
| Rules every agent follows | [`../../AGENTS.md`](../../AGENTS.md) |

The build plan is the authority on architecture and phases. This wiki is for what we
learn while executing it. It holds measurements and lessons, not status: what merged and when
is in the changelog (the old `docs/STATUS.md` was retired into it on 26 October 2026).

## The rule that matters most

**Never claim an accuracy change without a control arm** — same model, same machine, same
prompt set, same session. In the predecessor prototype, two of three attempts to improve
action-selection accuracy made it *worse*, and both were caught only because a baseline
ran alongside the treatment. At n≈50 the observed run-to-run noise on the control arm
alone was about 10 points; anything smaller than that is not a result.

## How to add a finding

When a measurement surprises you — especially when something intuitive turns out to be
wrong — write it into [`findings.md`](findings.md) **the same day**, with:

1. What you changed, precisely enough to reproduce.
2. The control and treatment numbers, and n.
3. The conditions: model, quantisation, hardware, whether other models were resident.
4. What you now believe, and what would change your mind.

Negative results are the most valuable entries here. A page that only records wins is a
page that lets the next person repeat every failure.
