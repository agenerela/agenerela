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
| How do I set my machine up? | [`environment.md`](environment.md) |

The build plan is the authority on architecture and phases. This wiki is for what we
learn while executing it.

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
