# Deciding without a generative model (researched and measured 12 November 2026)

Can something other than a generative language model choose the action and the target? Today
`qwen3.5:4b` does it: it reads the stimulus, the observations and the examples, and writes
`action`, `target` and `statement` under a JSON schema, in about 0.45 s. The lead asked for "the
decision making models/engines rather than LLMs", researched and tried on the RTX 5080. **Our
reading of that request**, which is the framework tech lead's and this page's: could a model built
for *choosing* (an embedding model, a reranker, a classifier) or a classic game engine (utility AI,
a behaviour tree, a planner) make the choice from a closed list, and how good is it on our own
prompts?

The page has two halves. [What exists](#what-exists-the-survey) is a survey: nothing in it was
measured by us, and every number there is a maker's or a paper's, cited. [What we
measured](#what-we-measured) is a run on our two evaluation sets, with `qwen3.5:4b` re-run as the
control in the same session on the same server (hard rule 2). The measured part is also in
[`findings.md`](findings.md), "Non-generative deciders against `qwen3.5:4b`".

**In short.** Nothing we could run comes close to the 4B as the whole decision. The best engine,
`bge-m3` embeddings with a small trained head on 90 reference examples we wrote, scored **279 of
389** on the interim set and **60 of 76** on the curated set, against the 4B's **330** and **70** in
the same session: 13 points down on both sets, beyond both noise floors (3.6 and 8). Every engine is
weakest where the survey said it would be: impossible requests, conversation and situations. As a
**first stage** the picture is better but not settled. Letting the engine answer only its most
confident quarter and sending the rest to the 4B scored 329 against 330, but that cut was read off the
evaluation set; the cascade tuned honestly, on our own 80 tuning prompts, sent 87% to the 4B. The
**hybrid**, where the engine chooses and the 4B writes only the statement, has the engine's accuracy
and saves only 0.06 s of the 4B's 0.54 s median, because reading the prompt, not writing the reply,
is most of a decision. **No reranker ran**: Ollama 0.40.14 has no rerank route, and both
Qwen3-Reranker GGUFs we pulled failed the yes/no route. Recommendation: no non-generative provider
now; the one idea worth an issue is an embedding first stage, measured as its own A/B.

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

## What exists: the survey

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

## What we measured

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
  and the request is rebuilt in Python, not taken from the C#. The scores match the sweeps anyway:
  330 of 389 (findings: 330) and 70 of 76 (69). That is a sanity check, not the control.
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
- **The hybrid**: the engine that scored best on the tuning set (`bge-m3` with the head) chooses, and
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
invisible.", word for word: the interim set was written by the same model. All 11 were rewritten,
and the check then passed: 142 lines against 465 prompts. Because one model writes near-identical
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
over the network on the interim set, median and 90th percentile.

| Engine | Curated /76 | Interim /389 | vs control, interim (points) | Action right /389 | Target right /389 | Impossible /64 | `none` expected /156 | Acting /233 | Median / p90 s | Resident MB |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---|---:|
| **`qwen3.5:4b`, control** | **70** | **330** | | 333 | 341 | 49 | 133 | 197 | 0.539 / 0.637 | 2,995 |
| `bge-m3:567m`, examples + head | 60 | 279 | −13.1 | 285 | 309 | 35 | 90 | 189 | 0.039 / 0.269 | 657 |
| `embeddinggemma:300m`, examples + head | 58 | 268 | −15.9 | 287 | 294 | 38 | 91 | 177 | 0.036 / 0.051 | 653 |
| `qwen3-embedding:0.6b`, examples + head | 58 | 264 | −17.0 | 281 | 294 | 36 | 76 | 188 | 0.035 / 0.052 | 939 |
| `bge-m3:567m`, examples (kNN) | 59 | 251 | −20.3 | 257 | 294 | 25 | 62 | 189 | 0.039 / 0.269 | 657 |
| `embeddinggemma:300m`, examples (kNN) | 53 | 249 | −20.8 | 268 | 285 | 34 | 73 | 176 | 0.036 / 0.051 | 653 |
| `qwen3-embedding:0.6b`, examples (kNN) | 54 | 216 | −29.3 | 233 | 283 | 14 | 34 | 182 | 0.035 / 0.052 | 939 |
| `embeddinggemma:300m`, descriptions | 46 | 220 | −28.3 | 223 | 281 | 32 | 109 | 111 | 0.036 / 0.051 | 653 |
| `bge-m3:567m`, descriptions | 42 | 200 | −33.4 | 200 | 252 | 54 | 140 | 60 | 0.039 / 0.269 | 657 |
| `qwen3-embedding:0.6b`, descriptions | 35 | 177 | −39.3 | 178 | 252 | 54 | 133 | 44 | 0.035 / 0.052 | 939 |
| Keyword parser and rules, no model | 51 | 205 | −32.1 | 207 | 286 | 58 | 131 | 74 | under 0.001 | 0 |

By category, the best engine against the control (interim, curated in brackets):

| Category | `bge-m3` + head | `qwen3.5:4b` |
|---|---:|---:|
| `simple_command` /65 (/9) | 55 (7) | 64 (9) |
| `command_with_target` /66 (/11) | 63 (10) | 66 (11) |
| `impossible_request` /64 (/9) | 35 (8) | 49 (9) |
| `conversation` /64 (/9) | 46 (9) | 60 (9) |
| `situation` /65 (/19) | 39 (10) | 40 (17) |
| `event` /65 (/19) | 41 (16) | 51 (15) |

What the rows say:

- **Commands are the easy part.** With examples, every embedder gets 56 to 64 of the 66 commands
  with a target, against the 4B's 66: a nearest-example search does name a place it is told about.
- **`none` is the hard part, as the survey predicted.** The thresholds trade one way or the other.
  Two zero-shot *descriptions* rows say `none` to 133 and 140 of the 156 `none` prompts, and to 54 of
  64 impossible requests, because they hardly ever act (44 and 60 of 233). The keyword parser does
  the same, 58 of 64 impossible, because an unknown noun matches no target. No threshold tuned on
  the tuning set got an engine both acting and refusing at the 4B's level.
- **Situations and events need reading, not matching.** On the curated set the best engine gets 10
  of 19 situations against the 4B's 17: "Something is wrong", with the reason in an observation, is
  not near any action description or example.
- **A trained head beats nearest examples, and both beat descriptions**, on all three models.
- **The three embedders are within noise of one another** once given examples and a head, 264 to
  279; `bge-m3`, the oldest and the only one with no instruction prefix, is first.

#### Cascades: the engine first, the 4B when it is unsure

Tuned on the tuning set (n = 80), then applied to the evaluation sets unchanged:

| Cascade | Curated /76 | Interim /389 | Sent to the 4B (interim) | Median / p90 s |
|---|---:|---:|---:|---|
| `bge-m3` + head, then the 4B | 69 | 330 | 87% | 0.572 / 0.817 |
| `embeddinggemma` + head, then the 4B | 66 | 319 | 61% | 0.529 / 0.649 |
| `qwen3-embedding` + head, then the 4B | 62 | 312 | 49% | 0.460 / 0.640 |
| `bge-m3` kNN, then the 4B | 66 | 289 | 31% | 0.051 / 0.624 |

At n = 80 the tuned thresholds are noisy, and the cascade that kept the 4B's score is the one that
sent it almost everything. **Read descriptively off the evaluation set, not tuned on it**, the shape
is clearer. Ranking each set by the `bge-m3` head's own margin and letting it answer only its most
confident share:

| Engine answers | 100% | 75% | 50% | 25% | 0% (the 4B alone) |
|---|---:|---:|---:|---:|---:|
| Interim /389 | 279 | 306 | 323 | 329 | 330 |
| Curated /76 | 60 | 65 | 67 | 69 | 70 |

So a confident quarter of the decisions could skip the 4B at no measurable cost on these prompts, and
half at a cost inside the interim floor (−1.8 points). That is a hypothesis for an A/B, not a result:
the cut was chosen by looking at these prompts. The engine is right on 25 interim prompts where the
4B is wrong, so a perfect arbiter would reach 355, but nothing here can tell those 25 apart.

#### The hybrid: the engine chooses, the 4B writes only the line

| | Curated | Interim |
|---|---|---|
| Correct | 60 (the engine's) | 279 (the engine's) |
| Statement call, median / p90 s | 0.395 / 0.486 | 0.419 / 0.532 |
| Engine and statement, median / p90 s | 0.435 / 0.550 | 0.477 / 0.753 |
| The 4B's whole decision, median / p90 s | 0.543 / 0.611 | 0.539 / 0.637 |
| Words per statement, hybrid / the 4B's own | 9.7 / 7.1 | 9.6 / 6.6 |
| Statements repeated word for word, hybrid / the 4B's own | 0 / 4 | 27 / 117 |

The 4B follows a decision it is given. Its line names the chosen target, and it argues for a wrong
choice as readily as for a right one ("I will secure the locked cave while you close the gate.").
The lines are longer and repeat less than when the 4B chooses, because the request differs, not
because they are better: nobody rated them. The time saved is small, since a statement-only call
still reads the whole system prompt, and the p90 is worse. With both models resident it would be
about 3.65 GB (657 + 2,995 MB, summed, not measured together).

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

Five, all onto the RTX 5080's Ollama. None was removed: the gateway cannot delete, and the lead can.

| Tag | Download | Resident, as run | Licence |
|---|---:|---:|---|
| `qwen3-embedding:0.6b` (Q8_0, 596M) | 639 MB | 939 MB at `num_ctx` 2048; 2,563 MB at its default context | Apache-2.0 ([card](https://huggingface.co/Qwen/Qwen3-Embedding-0.6B)) |
| `embeddinggemma:300m` (BF16, 308M) | 622 MB | 653 MB | Gemma Terms of Use (in the model's manifest) |
| `bge-m3:567m` (F16, 567M) | 1,158 MB | 657 MB | MIT (in the model's manifest) |
| `hf.co/ggml-org/Qwen3-Reranker-0.6B-Q8_0-GGUF` | 639 MB | did not run | Apache-2.0 ([card](https://huggingface.co/Qwen/Qwen3-Reranker-0.6B)) |
| `dengcao/Qwen3-Reranker-0.6B:Q8_0` | 639 MB | 742 MB; ran, output unusable | Apache-2.0, as Qwen's original; a community upload |

The control, `qwen3.5:4b` (3,324 MB download, 2,995 MB resident, Apache-2.0), was already on the
server.

## What a non-generative provider would need

`ILLMProvider` is one method, `RequestAsync(DecisionRequest)`, so a provider that embeds and compares
would fit behind it without the rest of the framework knowing. What it would have to work around today:

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
  similarity engine has none and sets `ReportsTokenProbabilities` false, and the guard then records
  that it could not run. The engine's own margin is a confidence of the same kind, but the framework
  has no field for a provider-supplied confidence, and the target threshold above already does the
  guard's job inside the engine.
- **A cascade is a provider that holds two providers**, an engine and a generative one, and asks the
  second when the first is unsure. It needs nothing beyond the above from the framework, but its
  threshold needs a tuning set of its own, written by the developer.
- **In Unity, not only through Ollama.** An embedding model as ONNX through Unity's Inference Engine
  would make it the one provider that needs no server; the tokenizer is the work. Not tried.

## Recommendation

- **No non-generative provider as the whole decision.** It scores 13 points below the 4B on both
  sets, beyond noise, and loses where a game most needs judgement: impossible requests, conversation
  and situations. That holds for every engine we could run.
- **An embedding first stage is the one idea worth an issue.** It costs about 0.04 s and 0.65 GB, and
  the descriptive curve suggests a confident quarter of the decisions could skip the 4B. It saves
  time only on the decisions it takes, it needs a reference bank and a tuning set from the developer,
  and its threshold must be tuned on new prompts. Measure it through `EvalRunner` as an A/B, with the
  guards on, before anyone builds it.
- **Not the hybrid on its own**: the statement call costs most of a decision.
- **Rerankers stay an open question** until Ollama has a rerank route, or a test runs llama.cpp's
  server directly.

## What was not tested

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
