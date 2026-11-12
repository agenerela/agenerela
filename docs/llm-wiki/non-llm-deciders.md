# Decision models, and other ways to decide without generating (researched and measured 12 November 2026)

Can something other than a generative language model choose the action and the target? Today
`qwen3.5:4b` does it: it reads the stimulus, the observations and the examples, and writes
`action`, `target` and `statement` under a JSON schema, in about 0.45 s.

The lead asked for research into "the decision making models/engines rather than LLMs", tested on the
RTX 5080, and then narrowed it, in his words: "I aprticularly want you to research those new models
Like Jev and the recent decision making engine by google", and "THe recent one from google is called
gemini decision 1. I want you to check on simillar local ones". So the centre of this page is a new
kind of model, the **decision model** (makers also say "System One model" or "typed decision
model"). These are Jev, Gemini Decision 1 and the open-weight models of the same kind. The
broader survey (embedding classifiers, rerankers, classic game AI) and its measurements on the 5080
are the background and the baseline.

**How the two names were identified.** Both were searched on 12 November 2026, and both exist under
those names.
- **Gemini Decision 1** (`gemini-decision-1`) is named in Google's launch post of 11 November 2026,
  [Gemini Decisions API](https://blog.google/innovation-and-ai/technology/developers-tools/gemini-decisions-api/),
  by Google DeepMind's product manager and principal scientist. Certain.
- **"Jev"** is TypeSafe AI's Jev, launched 15 September 2026 as "the first System One model". The
  sources are TypeSafe's documentation ([docs.typesafe.ai](https://docs.typesafe.ai/introduction))
  and press coverage of the launch ([InfoQ](https://infoq.com/news/2026/10/typesafe-ai-jev-released)).
  The lead's "models Like Jev" fits it exactly: by November there is a whole class of "Jev-like"
  models, and an open-source list of them ([awesome-decision-models](https://github.com/AnotiaWang/awesome-decision-models)).
  Very likely; no other model or company named Jev turned up.

**For the lead, at a glance.**

The decision models against `qwen3.5:4b`, curated 76 and interim 389, one session on the RTX 5080
(details in [The open decision models on our sets](#the-open-decision-models-on-our-sets-measured)):

| | Curated /76 | Interim /389 | Impossible /64 | Median s | Resident MB |
|---|---:|---:|---:|---:|---:|
| `qwen3.5:4b`, the framework's request (control) | 70 | 330 | 49 | 0.471 | 2,995 |
| `qwen3.5:4b`, asked as decision questions | 62 | 272 (−14.9) | 45 | 0.104 | 2,995 |
| `tev1:4b` | 56 | 248 (−21.1) | 49 | 0.104 | 5,457 |
| `strands-decider:2b` | 49 | 231 (−25.4) | 57 | 0.121 | up to 10,002 |
| `d1:3b` | 42 | 228 (−26.2) | 64 | 0.057 | 3,120 |

- **The question wording was not tuned.** All three models were asked in one wording, written before
  any run and tuned on nothing. Its `none` option carries a long description ("Choose this when
  nothing calls for an action, or when what is asked is impossible or names something not listed
  here"), which may push the models toward `none`. All three do refuse too much.
- **Eight models were pulled onto the server** for this research, 14.0 GB in all. They are listed
  with their sizes in [Models pulled](#models-pulled), with which ones you can remove to get the disk
  back. None was removed.

**In short.**
- **The two named models are hosted only.** Neither has weights, so neither can run on our card.
  Both answer a typed question about a state with probabilities over a closed list of options, in one
  forward pass, with no generated text and so no statement.
- **Open-weight models of the same kind exist and are in the Ollama library.** The small ones are
  Together AI's `tev1` (0.8B and 4B), Liquid AI's `d1` (3B) and AWS Strands' `strands-decider` (2B).
  Ollama serves them only on a new route, `/v1/systemone`, which the lead opened on the RTX 5080's
  gateway with [one line](#what-to-change-on-the-rtx-5080-for-decision-models).
- **On our prompts the open decision models are the weakest thing that ran.** Against the 4B's 330 of
  389 and 70 of 76 in the same session:
  - `tev1:4b` scored 248 and 56, `strands-decider:2b` 231 and 49, and `d1:3b` 228 and 42. All are
    beyond noise below the 4B, below the 4B asked the same questions, and below the best embedding
    engine.
  - They refuse commands: `d1` answers `none` to almost everything.
  - They are fast (0.06 to 0.12 s) and, as a first stage before the 4B, barely help.
  - This rests on one wording of the questions, not tuned ([below](#the-open-decision-models-on-our-sets-measured)).
- **What we could measure is the question format itself**, on our own `qwen3.5:4b`, scored from one
  forward pass's probabilities as these models score it. It is 3.2 times faster (0.140 s against
  0.453 s median) but 15 points worse on the interim set (272 against 330) and 10 on the curated set
  (62 against 70): it leans to `none` on simple commands. That gap is what decision-model training
  claims to close, and testing one of them is the obvious next step.
- **Of the non-generative engines we could run, none comes close as the whole decision.** The best,
  `bge-m3` embeddings with a small trained head, scored 271 and 63. As a **first stage** before the
  4B, with its threshold tuned on our own 80 tuning prompts, it kept 329 and 68 (within noise) while
  answering 28% of the interim decisions itself. One tuning on a small set, so it needs its own A/B.
- **No reranker runs on Ollama 0.40.14.**
- Recommendation: no decision-model provider and no non-generative provider as the decision. The one
  first stage worth an A/B is the `bge-m3` embedding cascade.

## The decision being replaced

One decision is: a free-form stimulus with a free-form label (`Player: "Meet me at the bridge."`,
`Event: "..."`, DR-008), a few observation lines, the actions available in the agent's state, and the
registered targets, each with a one-line description. The answer is one action, one target or
`no_target`, and a short statement. The hard cases, in our sets:

- **Impossible requests**, answered `none` / `no_target`: "go to the castle" where there is no
  castle, or a registered target the agent cannot use (`impossible_008`'s locked cave). 64 of the
  interim set's 389 and 9 of the curated 76.
- **Conversation** that asks for nothing: 38 of the interim set's 64 conversation prompts are `none`.
- **Targets named only in an observation**: "Something is wrong" with "Smoke rises from the docks".
- **State**: `stop_following` exists only while following, and the schema leaves it out
  otherwise; any engine gets that for free by masking the same way.

`none` is the expected answer for 156 of the interim set's 389 prompts and 30 of the curated 76. A
decider that never acts would score 40% and 39%.

## Decision models

Researched on 12 November 2026. **Nothing in this section was measured by us**: every number is a
maker's or a paper's, cited, and most are self-reported.

### What a decision model is

A decision model is given a **state** (text, often with images or other media) and one or more
**typed questions**:
- `choice`: pick one of a list of options, each with a description;
- `noul`: the probability that a statement is true;
- `score`: a place on an ordered rubric.

It returns a probability for every option, and a confidence, **without generating text**. Every
option is scored in one forward pass, so adding questions barely adds time. Jev's documentation
puts it as "No text generation, no parsing", with every question "evaluated in parallel and in
isolation against the same state in one go"
([docs](https://docs.typesafe.ai/introduction)). The makers position these models as a fast decision
layer inside software: routing, tool selection, policy checks, classification. They are not offered
as a replacement for chat.

**Our decision is exactly one of these questions.** The state is the label, the stimulus and the
observations. The action is a `choice` whose options are the available actions with their
descriptions, `none` included. The target is a second `choice` over the registered targets, with a
"nothing listed" option. What these models do not give is the `statement`.

### Gemini Decision 1 (Google DeepMind), hosted

From Google's launch post of 11 November 2026
([blog](https://blog.google/innovation-and-ai/technology/developers-tools/gemini-decisions-api/)):

- **What it is.** The model behind the new Gemini Decisions API, "a new endpoint for fast,
  closed-set decisions". Instead of writing an answer it "scores your candidate options in a single
  forward pass", returning probabilities and a confidence from 0 to 1 per answer.
- **Questions.**
  - `noul`, `choice` (2 to 255 options) and `score`.
  - Up to 1,000 independent questions per request, against one shared input of text, images, audio,
    video or PDF of up to 256K tokens.
- **Speed and price.**
  - 150 to 250 ms end to end, with 50 to 100 ms of model compute, for contexts up to 16K tokens.
  - Nearly flat from 1 to 100 questions.
  - $0.10 per million input tokens, nothing for output, and a no-cost tier in AI Studio.
- **Accuracy.**
  - Google says it "leads across the suite, with the lowest calibration error" on its routing,
    reasoning, ranking and multimodal benchmarks, but gives the numbers only in charts.
  - Its one named comparison is that Gemini 3.5 Flash-Lite costs "nearly 5x" more on the same suite.
- **Uses Google names.**
  - Gemini Notebook routes queries with it ("up to 40% lower latency").
  - Google Search uses it as a safety check on MCP tools.
- **Access.** Gemini API (v1beta) and AI Studio. Google Cloud "coming soon".
- **Weights.** Nothing says there are any; it is a hosted model.

**For us**, it cannot run on our card. A game calling it would send every decision to Google over
the network, about 0.2 s plus the round trip, where DR-001 puts inference in the game's own process. No API key was available to this research, so it was not tried.

### Jev (TypeSafe AI), hosted

- **What it is.** Launched 15 September 2026, as TypeSafe's "first System One model"
  ([docs](https://docs.typesafe.ai/introduction), [InfoQ](https://infoq.com/news/2026/10/typesafe-ai-jev-released)).
  Question types are `choice`, `score`, `noul` and `box`, a bounding box on an image.
  - TypeSafe's docs do not say how it is built or trained.
  - TypeSafe reports 70 to 500 ms per request; that is the maker's figure.
- **Access.** Early access, from a waitlist. Third-party pages list the endpoint as
  `POST https://api.typesafe.ai/v1/systemone`.
- **Weights.** None published: no architecture and no parameter count.
- **Independent evidence.**
  - Deußer and others ran it zero-shot on 37 datasets, 346,009 requests
    ([arXiv 2609.37647](https://arxiv.org/abs/2609.37647)). It beat Qwen3.8-27B on 27 of them, with
    none of Qwen's leads outside the bootstrap intervals, and beat Gemma-4-E4B on all 37. Its choice
    probabilities supported selective prediction.
  - In sequential games it did worse than Qwen. It completed 35% of ALFWorld tasks against 54.5%, and
    it repeated actions that had no effect.
  - An audit of the first week's 28 papers found that "the typed readout itself has not shown an
    independent accuracy advantage over comparable label-probability readouts"
    ([arXiv 2609.32160](https://arxiv.org/abs/2609.32160)). Reading option probabilities off a
    language model can do about as well; Jev's clear gains are latency and cost.

**For us**, the same as Gemini Decision 1: hosted only, so not on our card, and not tried.

### The open-weight ones ("similar local ones")

By November dozens of open models answer the same API. Ollama serves them on `POST /v1/systemone`,
which "follows TypeSafe's Jev API" ([`tev1` page](https://ollama.com/library/tev1)). The ones small
enough for an 8 GB card, from their Ollama library pages and model cards:

| Model | Maker | Base | Ollama tag, download | Licence | How it scores | Maker's evidence |
|---|---|---|---|---|---|---|
| Tev1 | Together AI | Qwen3.5-4B, fine-tuned ([card](https://huggingface.co/togethercomputer/Tev1-4B-experimental)) | `tev1:4b` 4.6 GB; `tev1:0.8b` 834 MB | "being finalized" on its card; Apache License in its Ollama manifest | keeps Qwen's output head and answers one option letter; 2 to 24 options; Ollama serves it on `/v1/systemone` only | 880 of 1,000 on its own development set; calibration "not comprehensively evaluated" |
| d1 | Liquid AI | LFM2.5-VL-3B ([blog](https://huggingface.co/blog/LiquidAI/open-d1)) | `d1:3b` 3.7 GB | LFM Open License v1.0 | trained to answer "in a single forward pass" with no tokens | best under 10B on the Jev Decision Index 0.2.1, 48.57; under 10 ms a question on an RTX 4090 |
| Strands Decider | AWS Strands Labs | Qwen3.5-2B-Base "with a trained decision head" | `strands-decider:2b` 2.0 GB | Apache-2.0 | a trained head; 4K context | 180 of 231 on JevBench; the Ollama page names Apple silicon, so it may not run on Linux CUDA |
| Perplexity Decider Flash | Perplexity | Qwen3.5-4B, distilled | `pplx-decider-flash:4b` 5.6 GB, above our 5 GB limit | Apache-2.0 | not stated on its page | Decision Index 0.3, 58.88 |
| Standard One | Standard Thinking | Ministral 3 3B and 8B ([card](https://huggingface.co/StandardThinking/StandardOne-8B)) | GGUF on Hugging Face, no library tag | Apache-2.0 | scores option letters in one pass, temperature-scaled; up to 255 options | JevBench hard tier 56.76% against Jev's 72.07% |
| Laya, GLiNER2.5-Decide | Convai, Fastino | ModernBERT-large 395M, DeBERTa-v3-large | `laya` in the library | Apache-2.0 | encoders | not checked |

Larger ones (Perplexity's 27B `pplx-decider`, Bespoke Labs' 9B `nimble`, Cloudflare's 27B `clef`)
would fit the 16 GB card at 4 bits, not an 8 GB one, and are beyond this research's pull limits.

**How one would make our decision.** Two `choice` questions in one request, from the request's
state:
- the action, over the available actions and `none`, each with its description;
- the target, over the registered targets and a "nothing listed" option.

A target answer of "nothing listed" makes the decision `none`. The developer's action and target
descriptions become the criteria. The confidence they return is a ready-made input for the grounding
guard and for a cascade to a generative model. The statement still has to come from somewhere else.

**How they did on our sets.** See
[below](#the-open-decision-models-on-our-sets-measured): worse than the 4B on both.

## Decision questions on our own 4B (measured)

No decision model could be run (see the next section), but the question format can be, on the model
we already have. The `decide` stage of
[`non_llm_decider_probe.py`](../../tools/benchmarks/non_llm_decider_probe.py) asks `qwen3.5:4b`
our decision as the decision models are asked it:

- **The prompt.** Tev1's recommended system line ("Evaluate the supplied decision task. ... Select
  exactly one listed option. Return only its letter"). Then Standard One's layout: `State:` (the
  agent, its state line, the targets, the observations, the labelled stimulus), `Question:`, and
  lettered `Options:` with descriptions.
- **Two questions.** The action, over the available actions and `none` ("Take no action. Choose this
  when nothing calls for an action, or when what is asked is impossible or names something not listed
  here."). Then, for a targeted action, the target, over the 11 targets and `no_target`.
- **One forward pass each.** `num_predict` 1; the answer is the first token's probability over the
  option letters. Every token that spells a letter adds to it ("A" and " A" both came back on 185
  questions), and the result is normalised over the letters seen. This is the "label-probability
  readout" that the audit above found typed decision models have not yet beaten.
- **Ollama returns only the top 20 tokens.** Every action question got all its letters back. On 16 of
  the 208 target questions, one to two of the 12 letters were missing, so those targets were chosen
  among the letters seen. Codex's review caught the first version of this stage keeping only the
  larger of two spellings of a letter. Fixed and re-run, it changed no answer; the numbers here are
  the re-run's.

Run 12 November, 11:08 to 11:14, on the RTX 5080 (Docker Ollama 0.40.14), one model resident, with
the control re-run in the same lock.

| | Curated /76 | Interim /389 | Impossible /64 | `none` expected /156 | Acting /233 | Said `none` when acting /233 | Median / p90 s |
|---|---:|---:|---:|---:|---:|---:|---|
| **`qwen3.5:4b`, the framework's request (control)** | **70** | **330** | 49 | 133 | 197 | 12 | 0.453 / 0.503 |
| `qwen3.5:4b`, decision questions | 62 | 272 (−14.9) | 45 | 117 | 155 | 44 | 0.140 / 0.259 |

| Category, interim (curated) | Decision questions | Control |
|---|---:|---:|
| `simple_command` /65 (/9) | 30 (5) | 64 (9) |
| `command_with_target` /66 (/11) | 59 (10) | 66 (11) |
| `impossible_request` /64 (/9) | 45 (8) | 49 (9) |
| `conversation` /64 (/9) | 43 (9) | 61 (9) |
| `situation` /65 (/19) | 46 (15) | 39 (17) |
| `event` /65 (/19) | 49 (15) | 51 (15) |

- **3.2 times faster at the median, and 15 points worse** on the interim set (10 on the curated
  set), beyond both floors. It writes one token per question where the control writes a JSON object
  with a statement, but its prompt differs too and the probe did not time the two parts of a request,
  so this says how much faster, not why.
- **It says `none` when it should act.** It said `none` on 44 prompts that wanted an action, against
  the control's 12; 20 of the 44 were simple commands ("Come with me." gets `none` 0.52, `follow_player`
  0.19). The untuned 4B leans to the last, longest option. This is what a decision model's training
  would have to fix, and what the bare question format does not.
- **Situations went the other way**: 46 of 65 against 39, +10.8 points, just beyond the interim
  set's per-category floor of about 9. A question that names the choice may suit a situation better
  than "Choose exactly one action". One category, one run: a lead, not a result.
- **No better on impossible requests** (45 against 49), the case a closed-set scorer is sometimes
  assumed to be good at.
- **A cascade inside one model does not pay.** We tried the readout first and the full request when
  its action margin was low, tuned on the 80 tuning prompts. It sent 85% of the interim decisions on
  to the full request, scored 321 (within noise), and took longer overall (0.582 s median against 0.453).

So the format alone, on a model not trained for it, is fast and not good enough. Whether a model
**trained** for it closes the 15 points is the open question. `tev1:4b`, Qwen3.5-4B fine-tuned for
exactly this, answered it like for like on its own route: it did not close them (next section).

## The open decision models on our sets (measured)

The lead applied the gateway change on 12 November, and the three pulled decision models were run on
their own route, `/v1/systemone`, against `qwen3.5:4b` in one lock session from 14:38 to 14:50 (RTX
5080, Docker Ollama 0.40.14, one model resident at a time). The probe's `systemone` stage sends each
prompt as one request with two `choice` questions, the action and the target:
- the same state, criteria and `none` wording as the decision questions above;
- a targeted action whose target answer is `no_target` becomes `none`.

The control is the framework's request, and the 4B's decision-question readout ran again beside it.
Each model answered a ten-prompt slice first, and scored as the labels mean. `ollama_remote.py
systemone tev1:4b` answered `choice move_to` at 0.08 s once loaded, so the check in the host
checklist works.

| | Curated /76 | Interim /389 | vs control, interim | Impossible /64 | `none` expected /156 | Acting /233 | Said `none` when acting /233 | Median / p90 s | Resident MB |
|---|---:|---:|---:|---:|---:|---:|---:|---|---:|
| **`qwen3.5:4b`, framework request (control)** | **70** | **330** | | 49 | 133 | 197 | 12 | 0.471 / 0.577 | 2,995 |
| `qwen3.5:4b`, decision questions | 62 | 272 | −14.9 | 45 | 117 | 155 | 44 | 0.104 / 0.190 | 2,995 |
| `tev1:4b` | 56 | 248 | −21.1 | 49 | 123 | 125 | 69 | 0.104 / 0.124 | 5,457 |
| `strands-decider:2b` | 49 | 231 | −25.4 | 57 | 132 | 99 | 61 | 0.121 / 0.178 | 3,908 to 10,002 |
| `d1:3b` | 42 | 228 | −26.2 | 64 | 156 | 72 | 154 | 0.057 / 0.070 | 3,120 |

| Category, interim (curated) | `tev1:4b` | `strands-decider:2b` | `d1:3b` | Control |
|---|---:|---:|---:|---:|
| `simple_command` /65 (/9) | 26 (4) | 27 (4) | 29 (4) | 64 (9) |
| `command_with_target` /66 (/11) | 42 (4) | 41 (10) | 26 (1) | 66 (11) |
| `impossible_request` /64 (/9) | 49 (9) | 57 (9) | 64 (9) | 49 (9) |
| `conversation` /64 (/9) | 38 (9) | 41 (8) | 42 (9) | 61 (9) |
| `situation` /65 (/19) | 45 (15) | 33 (10) | 35 (13) | 39 (17) |
| `event` /65 (/19) | 48 (15) | 32 (8) | 32 (6) | 51 (15) |

- **All three are beyond noise below the 4B on both sets**, by 21 to 26 points on the interim set
  and 18 to 37 on the curated set. All three are also below the 4B asked the same questions (272),
  and below `bge-m3` with a trained head (271).
- **They refuse too much.** `d1` answered `none` to every prompt labelled `none` and to 154 of the
  233 that wanted an action: its 64 of 64 impossible requests are what an agent that never acts gets.
  `strands-decider` refused 57 of 64 impossible requests, +12.5 points on the control and beyond the
  category's floor of about 9, but it also refused 61 commands and situations. `tev1` ties the
  control on impossible requests (49) and loses everywhere else except the interim situations (45
  against 39).
- **Commands are where they fail**, the case a generative model finds easiest: 26 to 29 of 65 simple
  commands against 64. "Come with me." gets `none` from `tev1` (0.69) and `d1` (0.63).
- **As a first stage they barely help.** With the margin threshold tuned on the 80 tuning prompts,
  the cascade from each to the 4B sent most decisions on: `tev1` 93%, giving 318 (−3.1); `d1` 96%,
  giving 330; `strands-decider` 76%, giving 323 (−1.8). That is no better than the `bge-m3` cascade
  above, which answered 28% itself and kept 329.
- **Fast once loaded**: 0.06 to 0.12 s median for both questions in one request, against 0.47 s.
  The first request loaded the model in 2.2 to 4.7 s.
- **Resident size.** `tev1:4b` is a Q8_0 build at 5.5 GB, nearly twice the 4B's Q4. `strands-decider`
  ran on Ollama's MLX runner in the Linux container, despite its page naming Apple silicon, but grew
  from 3.9 GB in the slice to 10.0 GB in the full run, too big for an 8 GB card beside a game.

**What the numbers rest on.** One wording of the questions, written before any run and tuned on
nothing:
- the state lists every target and the observations;
- the `none` option reads "Take no action. Choose this when nothing calls for an action, or when
  what is asked is impossible or names something not listed here".

The makers suggest a `none` option. A shorter `none`, or no description at all, might shift all three
towards acting. Tuning the wording on the 80 tuning prompts is the fair next step, and was not done.
The `strands-decider` and `d1` cards also advise fine-tuning for a task's own decisions.

## What it took to run the open decision models

- **The pulls.** The lead approved three more beyond the first five on 12 November; they are in
  [Models pulled](#models-pulled). `pplx-decider-flash:4b` (5.6 GB, over the 5 GB limit) was not
  pulled and is not tested.
- **The `/v1/systemone` route on the gateway.** The gateway passes `/api/chat`, `/api/generate`,
  `/api/embed`, `/api/tags`, `/api/ps`, `/api/show` and `/api/pull`, and nothing else. `d1` and
  `strands-decider` have trained heads that produce no tokens, so they answer only on
  `/v1/systemone`. One route in the gateway's `Caddyfile` on the RTX 5080 machine opens it: see
  [What to change on the RTX 5080](#what-to-change-on-the-rtx-5080-for-decision-models). That is a
  change to the server, the lead's to make, and this research did not work around it.
  - **No way round it.** Ollama gives each of the three only the `decision` capability, and refuses
    them on `/api/chat` and `/api/generate`, `tev1` included, though it keeps a normal output head on
    its model card. Each refusal was checked once under the lock on 12 November, and the gateway
    answered 403 on `/v1/systemone`.
- **The route.** The lead applied the Caddyfile change in the next section on 12 November. Before
  any run, the tech lead's session checked that an empty request to `/v1/systemone` now reaches Ollama
  (HTTP 400, "questions must contain 1 to 64 fields", where the gateway had answered 403).
- **`strands-decider` loaded** although Ollama pulled it as an MLX safetensors build (`runner: mlx`)
  and its page names Apple silicon.

## What to change on the RTX 5080 for decision models

For the person at the RTX 5080 machine. Everything here was checked against the repository's
`tools/ollama-server/` files and Ollama's release notes on 12 November 2026. The lead applied it the
same day, and the decision models then ran through it (above).

**(c) No Ollama upgrade is needed.** `/v1/systemone` arrived in Ollama **0.35.0** ([release notes,
28 September 2026](https://github.com/ollama/ollama/releases/tag/v0.35.0)): "decision models" on a
new `/v1/systemone` endpoint "based on TypeSafe's Jev API", launched with Nimble and Tev1. The
compose file pins **0.40.14**, later than that, so the route is already in the container. The gateway
is the only thing in the way. If the check in step 4 says the route itself is missing (an Ollama
404 that does not name a model), that would mean a newer Ollama. An upgrade is its own pull request
(AGENTS.md; `docker-compose.yml`'s comment on `image:`), because every result is tied to the version,
so it would be a separate decision.

**(d) No environment variable is needed.** The release notes name none, and the `ollama` service's
environment stays as it is.

**Steps**, in PowerShell or Git Bash on the RTX 5080 machine, in the repository's `tools/ollama-server`
folder:

1. **Get the change.** Either check out this pull request's branch (`non-llm-deciders`), or make the
   one edit by hand in `Caddyfile`. **(a)** The line today is:

   ```
   	@api path /api/version /api/tags /api/ps /api/show /api/chat /api/generate /api/embed /api/pull
   ```

   The new line:

   ```
   	@api path /api/version /api/tags /api/ps /api/show /api/chat /api/generate /api/embed /api/pull /v1/systemone
   ```

   It is the same line with ` /v1/systemone` at the end. Caddy's `path` matcher compares whole paths,
   so this opens that one route and no other `/v1` path. The pull request also updates the two comments
   that describe the routes.

2. **Check the file before applying it**, inside the running gateway, which sees the host's file
   through its bind mount:

   ```
   docker compose exec gateway caddy validate --config /etc/caddy/Caddyfile --adapter caddyfile
   ```

   It should end with `Valid configuration`.

3. **(b) Apply it, recreating only the gateway.** The service is `gateway`, its container
   `agenerela-ollama-gateway`; `ollama` is left alone, so the models stay loaded and nothing else on
   the machine is touched:

   ```
   docker compose up -d --force-recreate --no-deps gateway
   ```

   `--force-recreate` because the Caddyfile is a bind-mounted file that Caddy reads only when it
   starts, and a plain `up -d` sees no change to the compose file. `--no-deps` keeps `ollama` out of
   it. `docker compose ps` should then show both containers up.

4. **(e) Check it worked**, from the laptop, under the 5080 lock like any request, once a decision
   model is pulled (`python tools/ollama-server/ollama_remote.py pull tev1:4b`):

   ```
   python tools/ollama-server/ollama_remote.py systemone tev1:4b
   ```

   This is a new `ollama_remote.py` command in this pull request. It asks one `choice` question and
   prints the choice and its probabilities:

   | Output | Meaning |
   |---|---|
   | `tev1:4b: choice move_to, probabilities {...}` | It works |
   | `/v1/systemone is not exposed through the gateway` | The gateway still runs the old file: repeat step 3 |
   | An error naming the model | The route works, but that model has not been pulled |

**(f) What the route changes for security.**
- A key holder can already run any pulled model through `/api/chat` and `/api/generate`. The new route
  adds the same kind of thing: inference on pulled models, in the decision-model format.
- It adds no way to create, delete, copy or push a model, or to upload a blob, and opens no other
  `/v1` route.
- Two differences from `/api/chat`:
  - One request can carry up to 64 questions, which Ollama scores together. That is more work per
    request, but within the same use of the GPU.
  - Decision models such as `d1` take base64 images, as `/api/chat` already does for vision models.
- The gateway puts no limit on request size for any route, and Ollama's `tev1` page gives 64 KiB as
  the route's own limit.
- The key is still required, and the access log still drops headers.

**One model may not run on this machine at all.** `strands-decider`'s Ollama page says it "requires
Ollama 0.40.0 or later on Apple silicon Macs", and its tags are MLX and NVFP4 builds. The Linux
container on the 5080 may refuse it whatever the gateway allows. `tev1` and `d1` give no platform
limit on their pages.

## Background: other ways to decide without generating (survey)

Researched on 12 November 2026 from makers' pages, model cards, GitHub and arXiv. **No number in
this section was measured by us.**

**One thing holds for every family.** None of them writes a statement. A non-generative chooser
either leaves the statement empty, uses lines the developer wrote, or hands its choice to a
language model that writes only the line (the hybrid below).

### 1. Embedding models as classifiers

Embed the situation (label, stimulus and observations) once and compare it, by cosine similarity,
with each action's description and each target's description ("nearest description", zero-shot),
or with a bank of example stimuli per action ("nearest example", kNN), or feed the embedding to a
small trained head (logistic regression; SetFit fine-tunes the encoder first and then trains the
head, [arXiv 2209.11055](https://arxiv.org/abs/2209.11055)). Action and target are two separate
searches. A target named in an observation is found only if the observation is part of what is
embedded, and nothing links "it" in the stimulus to the observation: similarity is not reasoning.

**`none` is the known weak point.** An embedding always has a nearest candidate, so `none` comes
only from a threshold. That is the out-of-scope problem CLINC150 was built to test, whose authors
found classifiers do well in scope but "struggle to identify out-of-scope queries"
([arXiv 1909.02027](https://arxiv.org/abs/1909.02027)); kNN with a threshold is the standard
baseline ([arXiv 2010.13009](https://arxiv.org/abs/2010.13009)). Ours is harder than CLINC's:
"go to the castle" is in scope for `move_to` and out of scope only for its target.

**Needs from the developer:** the descriptions already registered (zero-shot), or better, a handful
of example stimuli per action, `none` included, and a threshold tuned on labelled examples that are
not the evaluation prompts.

| Model | Ollama tag, download | Licence | Maker's standing |
|---|---|---|---|
| Qwen3-Embedding | `qwen3-embedding:0.6b` 639 MB; `:4b` 2.5 GB ([library](https://ollama.com/library/qwen3-embedding)) | Apache-2.0 ([card](https://huggingface.co/Qwen/Qwen3-Embedding-0.6B)) | MTEB multilingual 64.33 (0.6B), 69.45 (4B); takes a task instruction on the query |
| EmbeddingGemma | `embeddinggemma:300m` 622 MB ([library](https://ollama.com/library/embeddinggemma)) | Gemma terms ([card](https://huggingface.co/google/embeddinggemma-300m)) | MTEB multilingual v2 61.15; has a `task: classification` prompt |
| bge-m3 | `bge-m3:567m` 1.2 GB ([library](https://ollama.com/library/bge-m3)) | MIT ([card](https://huggingface.co/BAAI/bge-m3)) | multilingual retrieval; no prompt prefix |
| nomic-embed-text, mxbai-embed-large, snowflake-arctic-embed2, granite-embedding | 63 MB to 1.2 GB | Apache-2.0 (granite, arctic; the others per their cards, not re-checked) | not pulled |

**Where it runs.** Through Ollama's `/api/embed`, yes. In Unity, also: Unity's Inference Engine
(`com.unity.ai.inference` 2.6) runs ONNX opset 7 to 25 on CPU or GPU on every runtime platform
([manual](https://docs.unity3d.com/Packages/com.unity.ai.inference@2.6/manual/index.html)), and small
sentence encoders have been packaged for it; the tokenizer then has to be written in C#. The
classifier itself, cosine and kNN or a linear head, is a few lines of C#.

### 2. Rerankers and cross-encoders

A cross-encoder reads the situation and *one* candidate together and scores the pair, so the two
attend to each other: more accurate than embedding similarity, at one model pass per candidate
(about 5 actions and 11 targets here). Qwen3-Reranker (0.6B, Apache-2.0) is a decoder prompted to
answer "yes" or "no", scored as P(yes); Qwen report MTEB-R 65.80 for it against 57.03 for
bge-reranker-v2-m3 ([card](https://huggingface.co/Qwen/Qwen3-Reranker-0.6B)). bge-reranker-v2-m3
has a classification head instead ([card](https://huggingface.co/BAAI/bge-reranker-v2-m3)). The
jina rerankers are CC-BY-NC-4.0, which rules them out for anything shipped
([v3 card](https://huggingface.co/jinaai/jina-reranker-v3)).

**Ollama has no rerank endpoint.** Its API documents none
([API](https://docs.ollama.com/api/introduction)); the tracking issue,
[ollama#3368](https://github.com/ollama/ollama/issues/3368), is open, and the two pull requests that
add one are unmerged. The one route left is `/api/generate` in raw mode with `logprobs`, reading
P("yes") against P("no") for a yes/no reranker. **We tried it, with two GGUFs, and neither works
on Ollama 0.40.14**: see [What we measured](#rerankers-two-tried-neither-runs-on-ollama). Outside
Ollama, llama.cpp's own server has `--reranking` and a `/v1/rerank` route, and vLLM serves
Qwen3-Reranker as a classifier; none of those is reachable through our gateway.

### 3. Encoder classifiers, zero-shot NLI, and small tool-choosing models

- **Zero-shot NLI** takes the stimulus as the premise and each choice as a hypothesis ("The speaker
  wants the guard to go to the bridge"), scored by entailment: `facebook/bart-large-mnli`
  ([card](https://huggingface.co/facebook/bart-large-mnli), MIT),
  `MoritzLaurer/deberta-v3-large-zeroshot-v2.0`
  ([card](https://huggingface.co/MoritzLaurer/deberta-v3-large-zeroshot-v2.0)), GLiClass, all labels
  in one pass ([repo](https://github.com/Knowledgator/GLiClass)). In principle NLI can test "this
  cannot be done here" as a hypothesis, which is nearer what impossible requests need than
  similarity is; we found no measurement of that. Hypotheses grow as actions × targets.
- **Span picking for targets.** GLiNER extracts spans for labels given at run time
  ([repo](https://github.com/urchade/GLiNER), Apache-2.0, ONNX export): it would find "castle" in
  the stimulus or "wolf" in an observation, and a span that matches no registered target is a
  clean signal for `none`.
- **Trained intent and entity models**, such as Rasa's DIET
  ([arXiv 2004.09936](https://arxiv.org/abs/2004.09936)), need dozens of training examples per
  intent and retraining whenever the actions change.
- **Small tool-choosing models are generative.** FunctionGemma (`functiongemma:270m`, 301 MB, Gemma
  terms, [card](https://ai.google.dev/gemma/docs/functiongemma/model_card)) reports BFCL
  Irrelevance 73.7, the benchmark nearest our `none`, and is meant to be fine-tuned. Salesforce's
  xLAM-2 1B and 3B are CC-BY-NC-4.0, research only
  ([card](https://huggingface.co/Salesforce/xLAM-2-1b-fc-r)). They are small language models, not
  an alternative to one, and the task ruled out pulling general chat models.

None of the encoders in this family runs through Ollama, which serves only GGUF decoders and
embedders; most export to ONNX for Unity's Inference Engine (not tried).

### 4. Classic game decision engines

Utility AI scores each action from response curves over typed inputs and takes the highest (Dave
Mark's Infinite Axis Utility System, [GDC AI Summit talks](https://gameai.com/iaus.php)). Behaviour
trees pick by prioritised conditions. GOAP plans a sequence with A* over preconditions and effects:
F.E.A.R. ran on three states and a planner
([Orkin, GDC 2006](https://pages.cs.wisc.edu/~dyer/cs540/handouts/gdc2006_orkin_jeff_fear.pdf)).
HTN planners decompose tasks into methods.

**None of them reads text.** They read typed facts, so something has to turn
`Player: "Meet me at the bridge."` into `intent = move_to, target = bridge` first: an intent
classifier (families 1 to 3) and a target linker. Historically that was hand-written: Façade mapped
typed player text to a few dozen discourse acts with hand-written surface rules
([Mateas and Stern, TIDSE 2004](https://www.cp.eng.chula.ac.th/~vishnu/gameResearch/story_november_2005/MateasSternTIDSE04.pdf)),
and text adventures parsed verb and noun against a fixed vocabulary. Given typed facts, a utility
system is good at exactly what the 4B finds hard: "no castle is registered" is a missing target that
scores `move_to` zero, so `none` wins. Our keyword-parser row below stands in for that front end.
Microseconds, no model, the developer's own code, in-process in C#.

### 5. Learned policies

Unity ML-Agents trains policies from a reward in a simulator and runs them through the Inference
Engine ([repo](https://github.com/Unity-Technologies/ml-agents)); a decision transformer models
action sequences from offline trajectories ([arXiv 2106.01345](https://arxiv.org/abs/2106.01345)).
**Neither fits this choice.** The developer registers an arbitrary action set, and a policy needs a
reward, a simulator, many episodes and retraining whenever an action or target changes; it reads
vectors, not text, so the stimulus still needs a front end. RL fits the movement or combat under an
action already chosen, not the choice.

### 6. Hybrids

- **Chooser and writer.** A non-generative chooser picks action and target; a language model is told
  them and writes only the statement, or the game uses canned lines. The field order rule (action,
  target, statement) then holds by construction.
- **Cascade.** The cheap chooser answers when it is confident and defers to the language model
  otherwise. FrugalGPT reports matching a large model at up to 98% lower cost
  ([arXiv 2305.05176](https://arxiv.org/abs/2305.05176)); RouteLLM reports over 2× cheaper routing
  without quality loss in some settings ([arXiv 2406.18665](https://arxiv.org/abs/2406.18665)).
  Here the impossible requests are exactly what a cascade should send up.
- **Semantic routers** do family 1 with a threshold as a product: aurelio-labs' semantic-router
  ([repo](https://github.com/aurelio-labs/semantic-router), MIT, Python) routes by example
  utterances and returns no route below a confidence it fits on labelled queries. A reference design,
  not a dependency (hard rule 6).

### Summary

| Family | Needs from the developer | Reads free text | `none` and impossible requests | Size, speed (makers') | Licence | Through Ollama | In Unity |
|---|---|---|---|---|---|---|---|
| Embedding (description, kNN, head) | descriptions; better, a few examples per action and a tuned threshold | as similarity | threshold only | 0.3 to 4B; one embed per decision | Apache, MIT; Gemma terms | yes, `/api/embed` | ONNX through Inference Engine (not tried) |
| Reranker | descriptions, an instruction, a threshold | jointly with each candidate | threshold | 0.6B; one pass per candidate | Apache (Qwen3, bge); NC (jina) | no endpoint; the logprob route failed here | ONNX cross-encoders (not tried) |
| Zero-shot NLI, GLiNER | hypotheses or labels | yes | can be posed as a hypothesis (unmeasured); a span with no registered target | about 0.4B | MIT, Apache | no | ONNX (not tried) |
| Small tool models | tool definitions, ideally fine-tuning | yes, generatively | BFCL Irrelevance 73.7 (FunctionGemma) | 270M to 3B | Gemma terms; NC (xLAM) | yes | through llama.cpp |
| Utility AI, BT, GOAP, HTN | hand-written scores and conditions | no, needs a front end | natively, for a target that is not there | microseconds | own code | n/a | yes |
| RL, decision transformer | reward, simulator, retraining per action set | no | what the reward teaches | small nets | Apache (ML-Agents) | no | yes |
| Cascade, chooser and writer | the chooser's needs and the language model | yes | the language model takes the doubtful ones | the chooser, plus the model on deferrals | per part | yes | yes |

## Baseline: embedding engines, cascades and the hybrid (measured)

12 November 2026, on the RTX 5080, Docker Ollama 0.40.14, over the network from the lead's laptop,
one model resident at a time, under the 5080 lock. The probe is
[`tools/benchmarks/non_llm_decider_probe.py`](../../tools/benchmarks/non_llm_decider_probe.py), its
examples are in [`non_llm_examples.json`](../../tools/benchmarks/non_llm_examples.json), and the
benchmarks README says how to run it.

### What was run

- **The control, `qwen3.5:4b`**, sent the framework's request as `EvalRunner` builds it for each
  prompt: PromptBuilder's system prompt with `VillageGuard`'s examples under the prompt's own label,
  the state line ModelSweepRun uses, one line per target, then the prompt's own observations; the flat
  schema with the actions available in the prompt's state; `think: false`, temperature 0, `num_ctx`
  4096, `logprobs`. **Where it differs from `EvalRunner`:** no guards run (neither grounding nor
  legality), so a reply a guard would have contained counts as given; one arm, not two interleaved;
  and the request is rebuilt in Python, not taken from the C#. The first run sent the refusal
  example as "Go and stand by the Atlantis." where `FewShotBuilder` writes "by Atlantis."; Codex's
  review caught it, and the run reported here uses the C#'s wording. The scores match the sweeps:
  330 of 389 (findings: 330) and 70 of 76 (69), in both runs. That is a sanity check, not the control.
- **Three embedding models**, each used three ways, all from one embedding of each situation (label,
  stimulus and the prompt's own observations, on one line):
  - *descriptions*: the nearest action description (the four action assets' and the framework's
    "Take no action") and the nearest target description, zero-shot;
  - *examples (kNN)*: the action whose three nearest reference examples are nearest on average;
  - *examples + head*: a softmax regression trained on the reference examples' embeddings.

  The target is always the nearest target description. `none` comes from two thresholds: the best
  acting action must reach one, and a targeted action's best target the other, or the answer is
  `none` / `no_target`. Both were tuned on the tuning set only.
- **A keyword parser with rules**, no model: action keywords and target names (with a few synonyms)
  written from the action descriptions and the reference examples. It stands in for the text front
  end a utility system or behaviour tree would need.
- **Cascades**, computed from the same answers: the engine answers when the margin between its best
  two actions reaches a threshold, tuned on the tuning set, and the 4B's answer is used otherwise.
  Latency is the engine's, plus the 4B's when it is asked.
- **The hybrid**: the engine that scored best on the tuning set (`embeddinggemma` with the head) chooses, and
  the 4B is told the choice and asked for the statement alone, in `BuildRefusalLine`'s shape (the
  decision's system prompt without examples, one user turn, a statement-only schema).
- **Rerankers**: tried, and neither ran (below).

**The reference and tuning examples are new** (hard rule 4). They were written by hand for this probe
by Claude (`claude-opus-5-5`) on 12 November, having read only the sets' counts and the ten prompts
of the first slice. There are 10 to 20 reference stimuli per action, `none` included (small talk,
impossible asks, and asks for things this agent does not have), with `{0}` filled by registered
targets' names as `FewShotBuilder` fills an example, giving 90 references; and an 80-prompt tuning set
in the evaluation format. `check-examples` tests every line against both sets with
`PromptSetChecks.MatchesExample`'s rule and a word-overlap check (Jaccard 0.8 or more). **Its first
run found 11 lines that matched interim prompts**, among them "Shadow me for a bit." and "Become
invisible.", word for word: the interim set was written by the same model. All 11 were rewritten.
**Codex's review then found that the check missed two cases**: the `{X}` placeholder never became a
wildcard ("Fight the {X}!" matches "Fight the bandits."), and the filled strings the engines embed
were never checked ("Take down the wolf." against "Take the wolf down."). With both fixed, five more
templates failed and were rewritten, and every stage was run again; the numbers here are that
second run's. The check now passes on 232 lines, templates and filled strings, against 465 prompts. Because one model writes near-identical
lines, a reference bank a model writes may sit closer to an evaluation set a model wrote than either
would to a player. The interim scores below may flatter the engines for that reason, never the
other way.

**The first slice**, ten prompts of each set and of the tuning set, proved the scoring. The control
answered all ten curated simple commands as labelled. The trained head first scored 0 of 10, a bug
(unit vectors gave it logits too small to learn from), fixed with a logit scale before the full run.

### Results

Correct answers, each against the control in the same session. The noise floor is about 3.6 points
at 389 and 8 at 76; every engine row below is beyond both. "Impossible" is the impossible-request
category; "`none` expected" is every prompt labelled `none`, conversation and situations included;
"acting" is every prompt labelled with an action. Latency is the probe's wall clock per decision
over the network on the interim set, median and 90th percentile. The embedders' medians moved between
the two runs (`embeddinggemma` 0.036 s in the first, 0.073 s in the second), so read them as "tens of
milliseconds over this network", not as a ranking.

| Engine | Curated /76 | Interim /389 | vs control, interim (points) | Action right /389 | Target right /389 | Impossible /64 | `none` expected /156 | Acting /233 | Median / p90 s | Resident MB |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---|---:|
| **`qwen3.5:4b`, control** | **70** | **330** | | 334 | 340 | 49 | 133 | 197 | 0.543 / 0.683 | 2,995 |
| `bge-m3:567m`, examples + head | 63 | 271 | −15.2 | 277 | 303 | 31 | 81 | 190 | 0.045 / 0.070 | 657 |
| `embeddinggemma:300m`, examples + head | 57 | 269 | −15.7 | 288 | 306 | 39 | 98 | 171 | 0.073 / 0.121 | 653 |
| `qwen3-embedding:0.6b`, examples + head | 58 | 259 | −18.3 | 276 | 289 | 30 | 72 | 187 | 0.050 / 0.079 | 939 |
| `bge-m3:567m`, examples (kNN) | 61 | 254 | −19.5 | 257 | 297 | 33 | 82 | 172 | 0.045 / 0.070 | 657 |
| `embeddinggemma:300m`, examples (kNN) | 53 | 248 | −21.1 | 264 | 286 | 33 | 71 | 177 | 0.073 / 0.121 | 653 |
| `qwen3-embedding:0.6b`, examples (kNN) | 54 | 214 | −29.8 | 231 | 281 | 14 | 34 | 180 | 0.050 / 0.079 | 939 |
| `embeddinggemma:300m`, descriptions | 46 | 220 | −28.3 | 223 | 281 | 32 | 109 | 111 | 0.073 / 0.121 | 653 |
| `bge-m3:567m`, descriptions | 42 | 200 | −33.4 | 200 | 252 | 54 | 140 | 60 | 0.045 / 0.070 | 657 |
| `qwen3-embedding:0.6b`, descriptions | 35 | 177 | −39.3 | 178 | 252 | 54 | 133 | 44 | 0.050 / 0.079 | 939 |
| Keyword parser and rules, no model | 51 | 205 | −32.1 | 207 | 286 | 58 | 131 | 74 | under 0.001 | 0 |

By category, the best engine against the control (interim, curated in brackets):

| Category | `bge-m3` + head | `qwen3.5:4b` |
|---|---:|---:|
| `simple_command` /65 (/9) | 54 (9) | 64 (9) |
| `command_with_target` /66 (/11) | 61 (11) | 66 (11) |
| `impossible_request` /64 (/9) | 31 (8) | 49 (9) |
| `conversation` /64 (/9) | 46 (9) | 61 (9) |
| `situation` /65 (/19) | 40 (10) | 39 (17) |
| `event` /65 (/19) | 39 (16) | 51 (15) |

What the rows say:

- **Commands are the easy part.** With examples, every embedder gets 56 to 61 of the 66 commands
  with a target, against the 4B's 66: a nearest-example search does name a place it is told about.
- **`none` is the hard part, as the survey predicted.** The thresholds trade one way or the other.
  Two zero-shot *descriptions* rows say `none` to 133 and 140 of the 156 `none` prompts, and to 54 of
  64 impossible requests, because they hardly ever act (44 and 60 of 233). The keyword parser does
  the same, 58 of 64 impossible, because an unknown noun matches no target. No threshold tuned on
  the tuning set got an engine both acting and refusing at the 4B's level.
- **Situations need reading, not matching.** On the curated set the best engine gets 10 of 19
  situations against the 4B's 17: "Something is wrong", with the reason in an observation, is not
  near any action description or example. On the interim set's situations the two are level (40
  and 39), so this is the curated set's harder situations, not every situation.
- **A trained head beats nearest examples, and both beat descriptions**, on all three models.
- **The three embedders are within noise of one another** once given examples and a head, 259 to
  271; `bge-m3`, the oldest and the only one with no instruction prefix, is first.

#### Cascades: the engine first, the 4B when it is unsure

Tuned on the tuning set (n = 80), then applied to the evaluation sets unchanged. "Sent to the 4B" is
per set:

| Cascade | Curated /76 | Interim /389 | Sent to the 4B, curated / interim | Median / p90 s, interim |
|---|---:|---:|---|---|
| `bge-m3` + head, then the 4B | 68 | 329 | 51% / 72% | 0.556 / 0.706 |
| `embeddinggemma` + head, then the 4B | 66 | 320 | 45% / 57% | 0.537 / 0.748 |
| `qwen3-embedding` + head, then the 4B | 60 | 313 | 29% / 50% | 0.469 / 0.674 |
| `bge-m3` kNN, then the 4B | 64 | 297 | 34% / 40% | 0.062 / 0.679 |

**The `bge-m3` cascade is within noise of the 4B on both sets** (−0.3 points on the interim set,
−2.6 on the curated set) while answering 28% of the interim decisions and 49% of the curated ones
itself. Its threshold came from 80 prompts, and the other three tuned no better than their engines
deserve: −2.6, −4.4 and −8.5 points on the interim set. So one cascade tuned honestly kept the 4B's
score; that is one tuning on a small set, not yet a property of cascades.

**Read descriptively off the evaluation set, not tuned on it**, the shape is the same. Ranking each
set by the `bge-m3` head's own margin and letting it answer only its most confident share:

| Engine answers | 100% | 75% | 50% | 25% | 0% (the 4B alone) |
|---|---:|---:|---:|---:|---:|
| Interim /389 | 271 | 306 | 331 | 331 | 330 |
| Curated /76 | 63 | 67 | 68 | 70 | 70 |

The engine's confident half costs nothing measurable on the interim set, and its confident quarter
nothing on either. Those cuts were chosen by looking at these prompts, so they are a hypothesis for
an A/B, not a result. The engine is right on 25 interim prompts where the 4B is wrong, so a perfect
arbiter would reach 355, but nothing here can tell those 25 apart.

#### The hybrid: the engine chooses, the 4B writes only the line

The chooser is `embeddinggemma` with the head, the engine with the best tuning-set score (72 of 80,
against `bge-m3`'s 71); the evaluation sets played no part in choosing it.

| | Curated | Interim |
|---|---|---|
| Correct | 57 (the engine's) | 269 (the engine's) |
| Statement call, median / p90 s | 0.375 / 0.467 | 0.380 / 0.482 |
| Engine and statement, median / p90 s | 0.448 / 0.540 | 0.463 / 0.574 |
| The 4B's whole decision, median / p90 s | 0.451 / 0.541 | 0.543 / 0.683 |
| Words per statement, hybrid / the 4B's own | 9.7 / 6.8 | 9.6 / 6.5 |
| Statements repeated word for word, hybrid / the 4B's own | 0 / 4 | 19 / 110 |

The 4B follows a decision it is given. Its line names the chosen target, and it argues for a wrong
choice as readily as for a right one ("I will secure the locked cave while you close the gate.").
The lines are longer and repeat less than when the 4B chooses, but the request differs too, and
nobody rated them. The time saved is small: the statement call alone took 0.38 s of the 4B's
0.54 s median on the interim set, and nothing on the curated set. The probe did not time reading
the prompt and writing the reply separately, so it cannot say where the rest goes. With both models
resident it would be about 3.65 GB (653 + 2,995 MB, summed, not measured together).

### Rerankers: two tried, neither runs on Ollama

- `hf.co/ggml-org/Qwen3-Reranker-0.6B-Q8_0-GGUF`: `/api/generate` answers "does not support
  generate". Through `/api/embed` it loads and returns a vector of zeros for every input: the
  conversion keeps the reranker's rank-pooling head, which Ollama does not run.
- `dengcao/Qwen3-Reranker-0.6B:Q8_0`, a community upload in the Ollama library: `/api/generate` in
  raw mode runs, but every token comes back with the same log-probability, −11.93, which is
  ln(1/151,936), a uniform distribution over Qwen3's vocabulary. Its output head carries nothing. Its
  uploader's page says reranking does not work on Ollama.

So on this server the cross-encoder family is untested, not measured bad. llama.cpp's own server,
run with `--reranking`, should load either; that is outside the gateway.

### Models pulled

Eight, all onto the RTX 5080's Ollama, 14,014 MB of downloads in all. None was removed: the gateway
cannot delete, and the lead can.

| Tag | Download | Resident, as run | Licence |
|---|---:|---:|---|
| `qwen3-embedding:0.6b` (Q8_0, 596M) | 639 MB | 939 MB at `num_ctx` 2048; 2,563 MB at its default context | Apache-2.0 ([card](https://huggingface.co/Qwen/Qwen3-Embedding-0.6B)) |
| `embeddinggemma:300m` (BF16, 308M) | 622 MB | 653 MB | Gemma Terms of Use (in the model's manifest) |
| `bge-m3:567m` (F16, 567M) | 1,158 MB | 657 MB | MIT (in the model's manifest) |
| `hf.co/ggml-org/Qwen3-Reranker-0.6B-Q8_0-GGUF` | 639 MB | did not run | Apache-2.0 ([card](https://huggingface.co/Qwen/Qwen3-Reranker-0.6B)) |
| `dengcao/Qwen3-Reranker-0.6B:Q8_0` | 639 MB | 742 MB; ran, output unusable | Apache-2.0, as Qwen's original; a community upload |
| `tev1:4b` (Q8_0, 4.3B, Qwen3.5) | 4,611 MB | 5,457 MB | Apache License in its Ollama manifest; its model card says the weights' licence is "being finalized" |
| `d1:3b` (Q8_0, 2.7B, LFM2; vision) | 3,731 MB | 3,120 MB | LFM Open License v1.0 (library page; none in the manifest) |
| `strands-decider:2b` (MXFP8 safetensors, MLX runner) | 1,975 MB | 3,908 MB after 30 prompts, 10,002 MB after 545 | its manifest carries a licence file; Apache-2.0 per its library page |

The last three were pulled at the lead's word on 12 November, after the first five.
`pplx-decider-flash:4b` (5.6 GB) was not pulled.

**Which can be removed to get the disk back.** No framework code, test or game uses any of the eight.
- **Remove first:** the two reranker GGUFs, `hf.co/ggml-org/Qwen3-Reranker-0.6B-Q8_0-GGUF` and
  `dengcao/Qwen3-Reranker-0.6B:Q8_0`, 1.3 GB together. Neither works on Ollama.
- **Remove unless the decision models are retried:** `tev1:4b`, `d1:3b` and `strands-decider:2b`,
  10.3 GB. They are needed only for the tuned-wording test named under the recommendation.
- **Remove unless the embedding first stage is A/B'd:** `qwen3-embedding:0.6b` and
  `embeddinggemma:300m`, 1.3 GB.
- **Keep `bge-m3:567m`** (1.2 GB) if that A/B is run; it is the engine it would use.

Removing is done on the machine itself, as the gateway refuses it:
`docker exec agenerela-ollama ollama rm <tag>` (`tools/ollama-server/Caddyfile`'s header comment).

The control, `qwen3.5:4b` (3,324 MB download, 2,995 MB resident, Apache-2.0), was already on the
server.

## What a non-generative provider would need

`ILLMProvider` is one method, `RequestAsync(DecisionRequest)`, so a provider that asks a decision model,
or one that embeds and compares, would fit behind it without the rest of the framework knowing. What it would have to work around today:

- **A decision-model provider would map the request onto `/v1/systemone`.** The request already
  holds what that API wants: the state (the identity, the observations and the labelled stimulus), the
  offered actions and targets, and, with a change below, their descriptions as criteria. Two
  `choice` questions in one call would return both choices with probabilities; a provider would fill
  `action` and `target` and leave `statement` empty or ask a generative provider for it. Behind
  `ILLMProvider` nothing else in the framework would know.
- **The candidates arrive as prompt text.** A provider gets the action and target ids from the
  schema's enums, and the stimulus and its label as fields (`StimulusText`, `StimulusLabel`). The
  action descriptions arrive only inside the system prompt, and only with `ListActionsAndTargets` on;
  the target descriptions only as observation lines. A non-generative provider would want them as
  data on `DecisionRequest`: each offered action's id, description and examples, and each offered
  target's id and description.
- **Examples would be registered on the action, as they are now.** `ActionDefinition` already carries
  an `ExampleStimulus` and a `CounterExampleStimulus` (answered `none`). A reference bank is the same
  field made a list, with `none` lines on the profile beside `IdleExampleStimulus`. Those are
  examples of each action, open strings the developer writes, not a list of situations the framework
  knows, so DR-008 holds. `PromptSetChecks` would check every line, as it checks the examples today.
- **The statement has to come from somewhere else**: empty, the developer's own lines per action, or
  a second request to a generative provider, which is what `BuildRefusalLine` already does for a
  refusal. The hybrid measured here is that second request.
- **The grounding guard would have nothing to read.** It reads token probabilities (DR-016). A
  similarity engine or a decision model has none and sets `ReportsTokenProbabilities` false, and the
  guard then records that it could not run. A decision model's probability per target, or an engine's
  margin, is a confidence of the same kind, but the framework has no field for a provider-supplied
  confidence, and the target threshold above already does the
  guard's job inside the engine.
- **A cascade is a provider that holds two providers**, an engine and a generative one, and asks the
  second when the first is unsure. It needs nothing beyond the above from the framework, but its
  threshold needs a tuning set of its own, written by the developer.
- **In Unity, not only through Ollama.** An embedding model as ONNX through Unity's Inference Engine
  would make it the one provider that needs no server; the tokenizer is the work. Not tried.

## Recommendation

- **No decision-model provider.** On our prompts the three open decision models scored 21 to 26 points
  below `qwen3.5:4b` on the interim set, refused commands, and gained almost nothing as a first stage.
  If one is tried again, tune the question wording, the `none` option above all, on new prompts
  first: the run here used one wording, untuned.
- **Not Gemini Decision 1 or Jev** for the framework's default: both are hosted only, and every
  decision would leave the game's machine.
- **No non-generative provider as the whole decision.** The best engine scores 15 points below the
  4B on the interim set and 9 on the curated set, beyond noise, and loses where a game most needs
  judgement: impossible requests, conversation and situations. That holds for every engine we could run.
- **An embedding first stage is the one idea worth an issue.** It costs about 0.05 s and 0.65 GB. One
  cascade, tuned on 80 new prompts, kept the 4B's score while answering 28% of the interim decisions
  itself, and the descriptive curve says up to half might be possible. It saves time only on the
  decisions it takes, it needs a reference bank and a tuning set from the developer, and its
  threshold must be tuned on new prompts. Measure it through `EvalRunner` as an A/B, with the guards
  on and more than 80 tuning prompts, before anyone builds it.
- **Not the hybrid on its own**: the statement call alone took 0.38 s of the 4B's 0.54 s.
- **Rerankers stay an open question** until Ollama has a rerank route, or a test runs llama.cpp's
  server directly.

## What was not tested

- **Gemini Decision 1 and Jev**: hosted only, and no key was available.
- **Other wordings of the decision questions**, a shorter or empty `none` criterion above all, and
  `pplx-decider-flash:4b` (5.6 GB, over the pull limit), `tev1:0.8b`, and the 9B and 27B decision
  models.
- Any reranker or cross-encoder (none runs on this Ollama), zero-shot NLI, GLiNER, GLiClass, DIET, and
  FunctionGemma or any other small generative tool model.
- `qwen3-embedding:4b`, and fine-tuning the encoder (SetFit) rather than training a head on a frozen one.
- Anything inside Unity: no ONNX model was loaded through the Inference Engine.
- The guards: the control ran without them, and so did every engine.
- Statement quality: nobody rated the hybrid's lines, only their length and repetition.
- Another agent or game. Both sets are the village guard's, five actions and eleven targets; a
  strategy game's moves read differently, and a larger action set would change every number here.
- More reference examples, examples written by a person rather than a model, and a tuning set larger
  than 80.
