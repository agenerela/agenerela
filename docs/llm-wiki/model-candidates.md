# Model candidates: what might beat `qwen3.5:2b` (researched 4 November 2026)

A researched list of open-weight models for the Phase 4 runner (#145) to test against
`qwen3.5:2b`, in four VRAM tiers. **The 8 GB tier is the default**, the size of the two models
we run, and comes first. Below it are [high-end tiers](#high-end-gpu-tiers-12-gb-16-gb-24-gb-and-up)
for players with 12, 16 and 24 GB or more, so the framework can offer a "high-end GPU" option
beside the default. **Nothing on this page was measured by us.**
Every benchmark number is a maker's or a third party's claim, cited below. No model was pulled,
run or timed for this page, and no ranking here is an accuracy result (hard rule 2). A
candidate earns a place in [`findings.md`](findings.md) only through an A/B run.

**Measured since.** Every model this page led us to pull was run on the evaluation set on
9 November 2026: [`findings.md`](findings.md), "Thirteen models on the evaluation set". That entry,
not this page, is where the scores are. The same 13 ran again on the 389-prompt interim set, on the
RTX 5080: "Thirteen models on the interim set, on the RTX 5080".

**Against `qwen3.5:4b`, since.** The 4B led both sweeps, so a later section,
[4B-class candidates against `qwen3.5:4b`](#4b-class-candidates-against-qwen354b-researched-11-november-2026)
(researched 11 November 2026, also unmeasured), lists what might beat it in about 4 GB resident.

## The job a candidate has to do

From the build plan (§2.3 to §2.5, Appendix A, Phase 6b) and [`findings.md`](findings.md):

- **One small JSON decision per call**: `action` from an enum, then `target` from an enum with a
  `no_target` sentinel, then a short `statement` in character. Constrained by the JSON schema in
  Ollama's `format` (later, GBNF through LLMUnity), with a generated few-shot block. 24 to 42
  tokens out, about 210 to 250 in (findings, 25 October).
- **The grammar does the formatting; the model only has to choose.** So JSON-mode and
  format-following benchmarks are weak proxies here. What matters is choosing the right action
  and target, saying `none` / `no_target` when nothing fits, and writing a believable line. The
  closest public proxies are instruction following (IFEval, IFBench, Multi-IF), function
  calling with irrelevance detection (BFCL) and character writing (Creative Writing, EQ-Bench).
- **The model never reads the schema** (findings, 10 October): field and action descriptions do
  not reach a local model, so it works from the prompt and the examples alone. A call can list the
  actions and targets in the prompt (`DecideOptions.ListActionsAndTargets`, #190), but that is off
  by default and unmeasured.
- **Thinking must be off.** A reasoning field before `action` measured worst of five variants
  (Appendix A), and a game waits on every token. `OllamaProvider` sends `think: false`. A model
  that always thinks is out.
- **Token probabilities from before the grammar mask.** The grounding guard (DR-016) reads
  `logprobs` and `top_logprobs`. On Ollama 0.40.14 they are still from before the mask for
  `qwen3.5:2b` (findings, 3 November). That was checked on Qwen only.
- **Fits an 8 GB card beside a game.** `qwen3.5:2b` at Q8_0 (2.7 GB file) plus a minimal scene
  measured 4.2 GB of 8 GB (Appendix A). [`environment.md`](environment.md) rules out 9B-class
  models on 8 GB.
- **Shippable (Phase 6b).** A GGUF that LLMUnity's llama.cpp can load, under a license that
  lets a demo build bundle the weights. LLMUnity's latest release when checked was v3.0.3
  (8 March 2026), on llama.cpp b8209, whose notes name Qwen 3.5 support
  ([releases](https://github.com/undreamai/LLMUnity/releases)). An architecture newer than that
  may not load until LLMUnity updates.

### Why look at all: the 2B's own numbers in the mode we use

Qwen's model card reports `Qwen3.5-2B` at **IFEval 61.2 in non-thinking mode**, against 78.6
with thinking on. Our decisions run with thinking off, so the lower number is the relevant one.
On the same card, the older `Qwen3-4B-Instruct-2507` scores 83.4 non-thinking (maker's claim,
[Qwen3.5-2B card](https://huggingface.co/Qwen/Qwen3.5-2B)). Most makers publish only
thinking-mode numbers for small models, so their tables flatter what we would actually run.
That gap is the main reason this list exists, and also the main reason to distrust it.

## 8 GB tier (the default) — shortlist: test these first against `qwen3.5:2b`

In order. Each one is one arm against `qwen3.5:2b` in #145's sequential-arms mode (see "How
this feeds Phase 4" below).

1. **`Qwen3-4B-Instruct-2507`** (`qwen3:4b-instruct-2507-q4_K_M`, 2.5 GB). Non-thinking only,
   so there is no switch to get wrong. Apache-2.0. Qwen's own tables put it above
   `Qwen3.5-2B` on IFEval in the mode we use (83.4 against 61.2), and it reports the strongest
   writing numbers here (Creative Writing v3 83.5, WritingBench 83.4), which bear on the
   `statement`. The Q4 file is smaller than `qwen3.5:4b`'s (3.3 GB). Its architecture has been
   in llama.cpp since 2025. It is the safest first arm, and the closest test of "is the 2B the
   weak link?". It is older (2025) and text-only.
   **Measured since (5 November 2026):** see [`findings.md`](findings.md), "First model A/B". On
   Ollama 0.40.14 it wrote `statement` before `target`, so that run does not yet answer the
   question.
2. **`Granite 4.1 3B`** (`granite4.1:3b`, 2.1 GB). A dense instruct model with no thinking
   mode (Ollama lists tools only). Apache-2.0. IBM reports IFEval 82.30 and BFCL v3 60.80. Its
   architecture is plain dense Granite, long supported by llama.cpp, so it should load in
   LLMUnity. It is IBM's enterprise-tuned model, so the in-character line is the open question;
   IBM publishes no writing benchmark for it.
3. **`NVIDIA Nemotron 3 Nano 4B`** (`nemotron-3-nano:4b`, 2.8–2.9 GB). The only maker here
   that publishes **reasoning-off** numbers: IFEval-Prompt 82.8, BFCL v3 61.1, EQ-Bench3 63.2
   (the last bears on writing in character). Thinking is on by default and switches off through
   the chat template. A Mamba-2 hybrid, which should be fast per token. Two catches: the NVIDIA
   Nemotron Open Model License allows commercial use and redistribution with an attribution
   notice, but it is not Apache; and LLMUnity's support for the hybrid architecture is
   unverified.
4. **`MiniCPM5-2B`** (GGUF from `openbmb/MiniCPM5-2B-GGUF`, about 1.6 GB at Q4 by our estimate;
   no Ollama library tag, pull it through `hf.co/...`). The only true 2B-class challenger with
   Apache-2.0 weights and a standard `LlamaForCausalLM` architecture, so it is the most likely to
   load in any llama.cpp, LLMUnity's included. OpenBMB reports IFEval 86.7 and BFCL v4 66.6
   against 77.5 and 43.6 for `Qwen3.5-2B` in the same table, without saying which mode. Liquid's
   LFM3 card, a competitor's table, reports it at IFEval 87.83. How to turn its thinking off is
   not on its card: check the GGUF's chat template before the run. Third-party reviews call its
   general knowledge weaker than `Qwen3.5-4B`'s.
5. **`LFM3-1.7B`** (`lfm3:1.7b`, 1.1–1.7 GB). The newest (27 October 2026) and smallest, with
   the boldest claims: Liquid reports IFEval 96.66, BFCL v4 71.39 and ToolSandbox 80.36, above
   `Qwen3.5-9B` on its own card. Nobody independent has reproduced that yet. Thinking is on by
   default; `enable_thinking=False` turns it off, which the card warns "can be inconsistent". The
   LFM Open License v1.0 allows free commercial use only below USD 10 million annual revenue,
   which suits the course's demos but must be spelled out to any studio using the framework. Its
   architecture (convolution plus sliding-window attention) post-dates LLMUnity's llama.cpp
   b8209, so Phase 6b may have to wait for an LLMUnity update. Test it last of the five, as the
   long shot.

## 8 GB candidate table

Sizes are Ollama's download sizes when the library lists the quantization, otherwise our
estimate (marked *est.*), at about 0.6 bytes a parameter at Q4_K_M and 1.06 at Q8_0. Resident
VRAM is higher than the file: add the KV cache at 4096 tokens and compute buffers, a few hundred
MB for these sizes (Appendix A's 4.2 GB was the 2.7 GB file plus a minimal scene). Benchmarks are
all maker's or third party's claims; see the column note. "Thinking off" means for an Ollama
request and the GGUF chat template.

| Model | Maker | Released | Params | License, and can a game bundle the weights? | Ollama tag | GGUF | Q4 / Q8 file | Thinking off? | Claimed benchmarks (source) | Known weaknesses |
|---|---|---|---|---|---|---|---|---|---|---|
| **Qwen3.5-2B** (baseline) | Alibaba Qwen | Mar 2026 | 2B + vision | Apache-2.0, yes | `qwen3.5:2b` | yes | 1.9 / 2.7 GB | Off by default | IFEval 61.2 non-thinking, 78.6 thinking; BFCL-V4 43.6 (thinking) [Q1] | Weak IFEval with thinking off; card warns of thinking loops on the 2B |
| Qwen3.5-4B (the #151 row) | Alibaba Qwen | Mar 2026 | 4B + vision | Apache-2.0, yes | `qwen3.5:4b` | yes | 3.3 / 5.2 GB | **On by default**; `think: false` | IFEval 89.8, BFCL-V4 50.3 (mode not stated) [Q2] | Thinking on by default, so every request must carry `think: false` |
| Qwen3-4B-Instruct-2507 | Alibaba Qwen | 2025 (card gives no date) | 4.0B | Apache-2.0, yes | `qwen3:4b-instruct-2507-q4_K_M` | yes | 2.5 / 4.3 GB | No thinking mode | IFEval 83.4, BFCL-v3 61.9, Creative Writing v3 83.5, WritingBench 83.4 [Q3] | Older; text only; different tokenizer from Qwen3.5, so the guard's threshold needs re-checking |
| Granite 4.1 3B | IBM | 29 Apr 2026 | 3B dense | Apache-2.0, yes | `granite4.1:3b` | yes | 2.1 / ~3.4 *est.* GB | No thinking mode listed | IFEval avg 82.30, BFCL v3 60.80, ArenaHard 37.80 [G1] | No writing benchmark; a third-party listing calls its benchmarks weak |
| Granite 4.2 3B | IBM | 25 Aug 2026 | 3B dense | Apache-2.0, yes | `granite4.2:3b` | yes | 2.2 / ~3.5 *est.* GB | On by default; `enable_thinking=False` leaves an empty `<think></think>` | BFCL v4 52.41, IFBench 74.33 (mode not stated) [G2]; MiniCPM's table: IFEval 93.7 [M1] | A reasoning model; its thinking-off quality is unpublished |
| NVIDIA Nemotron 3 Nano 4B | NVIDIA | 16 Mar 2026 | 3.97B, Mamba-2 hybrid | NVIDIA Nemotron Open Model License: commercial use and redistribution with a notice, plus export terms; yes, with attribution | `nemotron-3-nano:4b` | yes | 2.8–2.9 GB / ~4.2 *est.* | On by default; `enable_thinking=False` | Reasoning off: IFEval-Prompt 82.8, BFCL v3 61.1, EQ-Bench3 63.2 [N1] | Not Apache; hybrid architecture untested in LLMUnity |
| MiniCPM5-2B | OpenBMB | ~6 Sep 2026 (third party) | 2.52B, Llama architecture | Apache-2.0, yes | none in the library; `hf.co/openbmb/MiniCPM5-2B-GGUF` | yes | ~1.6 / ~2.7 *est.* GB | Not documented on the card | IFEval 86.7, BFCL v4 66.6, τ²-Telecom 97.1 (mode not stated) [M1]; LFM3's table: IFEval 87.83 [L1] | Weaker general knowledge than Qwen3.5-4B; reported fragile in real agent tools [M2] |
| LFM3-1.7B | Liquid AI | 27 Oct 2026 | 1.73B, conv + attention hybrid | LFM Open License v1.0: free commercial use below USD 10M annual revenue; yes for the course, conditional for others | `lfm3:1.7b` | yes | 1.1 / ~1.8 *est.* GB | On by default; `enable_thinking=False`, "can be inconsistent" | IFEval 96.66, IFBench 73.03, BFCL V4 71.39 [L1] | Newest and least corroborated; revenue-capped license; may need a newer LLMUnity |
| Gemma 4 E2B | Google DeepMind | 2 Apr 2026 | 2.3B effective, 5.1B with embeddings, + vision, audio | Apache-2.0, yes | `gemma4:e2b` | yes | 4.6 / 6.0 GB | Off unless `<\|think\|>` is in the system prompt | Tau2 24.5 [E1]; MiniCPM's table: IFEval 31.4 [M1] | Large file for its size; third-party IF numbers conflict |
| Gemma 4 E4B | Google DeepMind | 2 Apr 2026 | 4.5B effective, 8B with embeddings | Apache-2.0, yes | `gemma4:e4b` | yes | 6.6 / 9.0 GB | As E2B | Tau2 42.2 [E1]; IFEval 90.02 per LFM3's table [L1], 44.4 per MiniCPM's [M1] | Too large for 8 GB beside a game |
| Ministral 3 3B Instruct 2512 | Mistral AI | Dec 2025 | 3.4B + 0.4B vision | Apache-2.0, yes | `ministral-3:3b` | yes | 3.0 / 4.5 GB | No thinking in the Instruct model | Arena Hard 0.305, WildBench 56.8; no IFEval or tool numbers [Mi1] | No instruction-following or tool numbers published; vision encoder adds size |
| Ministral 3 8B Instruct 2512 | Mistral AI | Dec 2025 | 8B | Apache-2.0, yes | `ministral-3:8b` | yes | 6.0 / 9.9 GB | No thinking in Instruct | Arena Hard 0.509, WildBench 66.8 [Mi1] | Too large for 8 GB beside a game |
| LFM2.5-2.6B | Liquid AI | Aug 2026 (third party) | 2.69B | LFM Open License v1.0 (as LFM3) | none for 2.6B | yes | ~1.6 / ~2.8 *est.* GB | **Always thinks**; the card gives no switch | IFBench 59.17, BFCL v4 56.88 [L2] | Always reasons before answering |
| SmolLM3-3B | Hugging Face | 8 Jul 2025 | 3B | Apache-2.0, yes | none in the library | yes | ~1.9 / ~3.3 *est.* GB | On by default; `/no_think` or `enable_thinking=False` | No-think: IFEval 76.7, BFCL 92.3 (older BFCL version) [S1] | Older; no library tag |
| Phi-4-mini-instruct | Microsoft | Feb 2025 | 3.8B | MIT, yes | `phi4-mini` | yes | ~2.5 / ~4.1 *est.* GB | No thinking mode | No IFEval or BFCL numbers published [P1] | Card warns it "could sometimes hallucinate function names"; older |
| Llama 3.2 3B Instruct | Meta | Sep 2024 | 3.2B | Llama 3.2 Community License: attribution, acceptable-use policy, 700M MAU cap; yes, with conditions | `llama3.2:3b` | yes | ~2.0 / ~3.4 *est.* GB | No thinking mode | IFEval 77.4, BFCL V2 67.0 [Ll1] | Two years old; superseded |

Column notes:
- **Benchmarks are not comparable across rows.** Makers pick different versions (BFCL v2, v3,
  v4), modes and harnesses, and the third-party tables disagree with each other by as much as
  60 points on one model (Gemma 4 E4B's IFEval: 90.02 in Liquid's table, 44.4 in OpenBMB's).
  Every table comes from a company with a model on the list.
- **Logprobs.** Ollama's native `/api/chat` returns `logprobs` and `top_logprobs` (0 to 20) for
  local models; a third-party document dates this to Ollama 0.12, and its Cloud and
  `/v1/chat/completions` routes do not return them ([#13638](https://github.com/ollama/ollama/issues/13638),
  [#16117](https://github.com/ollama/ollama/issues/16117)). No model-specific problem turned up
  in a search of Ollama's issues, but **"from before the mask" has been checked on
  `qwen3.5:2b` only**. Every candidate needs the scarecrow check from the 27 October findings
  entry before its guarded results mean anything.
- **`format` JSON schema.** Ollama compiles the schema into a sampling grammar for every model
  it runs, so all rows work with `format`. A third-party write-up reports that Ollama 0.34.4
  changed how a thinking model's reasoning combines with `format`
  ([Towards AI](https://pub.towardsai.net/ollama-0-34-4-lets-qwen-think-before-json-then-blocks-the-tool-call-5-of-6-parsers-tested-8313c8dfb089));
  with `think: false` this should not apply, but a model whose thinking cannot be fully turned
  off may still spend tokens before the JSON.

## Ruled out of the 8 GB tier, one line each

Models marked *(higher tier)* are too big for 8 GB but are weighed again in the
[high-end tiers](#high-end-gpu-tiers-12-gb-16-gb-24-gb-and-up).

- **Qwen3.5-0.8B**: Qwen's own non-thinking IFEval is 52.1, below the 2B.
- **Qwen3.5-9B** *(higher tier)*: 6.6 GB at Q4; `environment.md` rules out 9B-class models on
  8 GB.
- **Gemma 4 E2B**: 4.6 GB at Q4 on Ollama for 2.3B effective parameters (its vision and audio
  encoders and per-layer embeddings come along), and the third-party numbers conflict. First
  reserve if a text-only GGUF proves much smaller.
- **Gemma 4 E4B and 12B** *(12B: higher tier)*: 6.6 GB and 8.0 GB at Q4.
- **Ministral 3 3B**: plausible, but Mistral publishes no instruction-following or tool numbers
  for it, and its vision encoder costs a gigabyte at Q4. Second reserve.
- **Ministral 3 8B, Granite 4.1 8B, Granite 4.2 8B, LFM2.5-8B-A1B** *(higher tier)*: 5.2 to
  6.0 GB at Q4, too much beside a renderer on 8 GB.
- **Granite 4.2 3B**: a reasoning model whose thinking-off numbers are unpublished. Worth one
  arm only if Granite 4.1 3B does well.
- **Granite 4.0 Nano and Micro**: superseded by 4.1.
- **LFM2.5-2.6B**: always thinks, with no switch on its card (AGENTS.md: thinking before
  `action` measured worst).
- **LFM2.5-1.2B**: superseded by LFM3-1.7B.
- **SmolLM3-3B**: superseded by newer 3B models, and no Ollama library tag.
- **Phi-4-mini-instruct**: publishes no instruction-following or tool numbers, and warns of
  hallucinated function names.
- **Llama 3.2 3B**: two years old and superseded; its license adds conditions Apache does not.
  Meta has released no small Llama 4.
- **Qwen3-1.7B**: superseded by Qwen3.5-2B, and a hybrid-thinking model.
- **Aleph Alpha Kolibri-1**: 3.46B active, but a 78B mixture of experts.
- **LFM3's d1-2B "decision" models**: described only as returning "calibrated answers"; not
  enough is published to judge. Worth a read when the grounding guard's calibration is next
  revisited, since calibrated confidence is what DR-016 reads.

## 4B-class candidates against `qwen3.5:4b` (researched 11 November 2026)

**Why this section.** Since this page was written, `qwen3.5:4b` has become the model the team
leans on. On the RTX 5080 it scored 330 of 389 on the interim set against the 2B's 263, at
0.51 s a decision, and with personas at the GreyBoxStrategy table it took 0.46 s and 3.1 GB
resident (findings, 9 and 11 November). So this section asks a narrower question than the 8 GB
shortlist above: which open-weight models of about 3B to 5B parameters, or bigger ones that sit
in about 4 GB resident when quantised, are worth one arm against `qwen3.5:4b`. The job, and every
rule in "The job a candidate has to do", is unchanged. **Nothing here was measured by us**: no
model was pulled, run or timed for it. Every number is a maker's or a third party's claim, linked
in the sources under "4B-class section".

**The honest answer first.** No candidate's published numbers make a clear case for beating
`qwen3.5:4b` at choosing an action and a target. On the interim set it leads the next-best
4B-class model by 26 answers (findings, 9 November), and the one maker table that compares a new
model with it directly, NVIDIA's for Bolt E4B, has Qwen ahead on function calling (BFCL-v3,
Tau2), the closest public proxy for that choice. Where the new models claim an edge is the
`statement`: role-play and instruction-following sets. So the likeliest gain from this list is a
better line at about the same accuracy, not a better choice. That is still worth one arm,
because `gemma4:e2b-it-qat` already wrote the cleanest statements at the strategy table (findings,
11 November).

Every model already pulled on the RTX 5080 ran in the two sweeps of 9 November, and none
reached `qwen3.5:4b`, so everything on this shortlist is new to the server.

### Shortlist, in test order

Each is one arm in the shape of "How this feeds Phase 4" below, with **`qwen3.5:4b` as the
control** in place of the 2B. **None is pulled on the RTX 5080 yet: all four need a pull, which
needs the lead's yes.**

1. **Gemma 4 E4B, QAT** (`gemma4:e4b-it-qat`, a 6.1 GB download). Google, Apache-2.0, thinking off
   unless `<|think|>` is in the system prompt, and on E2B and E4B no empty thought block when it is
   off [E1]. The earlier sections ruled it out on its download size, but the download is not what
   sits in VRAM: `gemma4:e2b-it-qat` downloads at 4,135 MB and reported 1,578 MB resident
   (findings, 9 November), because most of a Gemma "E" model's weight is per-layer embeddings. By
   the same ratio the E4B would sit around 2.5 to 3.5 GB. That is arithmetic, not a measurement,
   and NVIDIA's 3.5 GB peak for Bolt E4B, the same architecture at Q4_K_M, points the same way
   [B4]. It is first because it is the cheapest arm to trust: the family already runs on our
   server and kept the field order, the E2B gave the best statements and nearly the 4B's persona
   effect at the strategy table (η² 0.713 against 0.841), and it is the base of the second
   candidate, so it is the control that tells whether NVIDIA's tuning of it adds anything. Its
   public instruction-following numbers conflict: IFEval 90.02 in Liquid's table [L1], 44.4 in
   OpenBMB's [M1], 45.3 in SparkLLM's [Sp4]; Google's card gives only IFBench 44 [E1]. **Ruled out
   if** it sits above about 4 GB resident, or scores below `qwen3.5:4b` beyond noise on simple
   commands and situations, which is where the 4B's lead is.
2. **NVIDIA Nemotron 3.5 Bolt E4B** (`nemotron-3.5-bolt:e4b`, 7.2–7.9 GB on Ollama with a vision
   projector and a speculative-decoding draft; the text GGUF `nvidia/NVIDIA-Nemotron-3.5-Bolt-E4B-4GB`
   is 6.18 GB at Q4_K_M). Released 23 October 2026, post-trained by NVIDIA from Gemma 4 E4B with
   reinforcement learning, aimed at "interactive personas" [B1]. It is the only new model whose
   maker compares it with `qwen3.5:4b` directly, in NVIDIA's own harness: in **non-reasoning mode**,
   our mode, RoleBench 67.05 against 53.66; in reasoning mode, IFBench 63.79 against 58.91 and
   InverseIFEval 46.84 against 30.98, but BFCL-v3 68.51 against 70.95 and Tau2 55.10 against 75.06
   [B1]. At Q4_K_M the gaps hold, and Qwen leads IFBench 66.67 to 64.00 [B2]. NVIDIA reports 3.5 GB
   peak and 133 tok/s on an RTX 5070 at Q4_K_M, about 3,000 tokens in and 50 out, through llama.cpp
   [B4]. Thinking is on by default and `enable_thinking: false` turns it off [B1]; that Ollama's
   `think: false` maps onto it is not documented [B3]. License OpenMDW-1.1, permissive, which asks
   a redistributor to keep the license text and notices, but which was still before the OSI and not
   on SPDX in August 2026 [OM]. **Two catches:** the card calls it "intended primarily for
   customization and post-training rather than direct production inference" [B1], and the Ollama
   tags are two weeks old, so whether our server's Ollama 0.40.14 loads them is unknown. **Ruled out
   if** `think: false` leaves reasoning tokens, it fails to load on 0.40.14, or it does no better than
   item 1, its own base.
3. **Spark-X2.6-4B** (`SparkLLM/Spark-X2.6-4B:q4_k_m`, 3.5 GB; `q8_0` 4.4 GB; a user namespace on
   Ollama, not the library). SparkLLM, released 21 October 2026, Apache-2.0, 4.11B [Sp1]. Its card
   claims IFEval 93.6, IFBench 75.8 and BFCL-V4 71.8, against 89.8, 59.2 and 50.3 for Qwen3.5-4B
   copied from Qwen's card, **all in thinking mode at its "high" level** [Sp4]; it publishes nothing
   with thinking off. Thinking is on by default; `enable_thinking: false` is documented for SGLang
   and vLLM only, and the Ollama page shows no thinking capability, so `think: false` may be ignored
   or refused [Sp3]. Its architecture (`spark2_5`) needs llama.cpp b10828 or later [Sp1], far past
   LLMUnity v3.0.3's b8209, so it cannot ship in-process until LLMUnity catches up. The biggest
   claims on this list, with the least independent support. **Ruled out if** thinking cannot be
   turned off through Ollama, which is the first pre-check.
4. **Granite 4.2 3B** (`granite4.2:3b`, 2.2 GB). Already in the 8 GB table above, which said it was
   worth one arm "only if Granite 4.1 3B does well". It did: `granite4.1:3b` tied for third on the
   interim set at 304 of 389, at the lowest latency of the top ten (findings, 9 November). IBM's
   4.2 is a reasoning model whose thinking-off quality is unpublished, and `enable_thinking=False`
   leaves an empty `<think></think>` [G2]; in the K2-Horizon table it scores BFCL v4 50.8, in high
   reasoning, against 55.7 for Qwen3.5-4B [K1]. Apache-2.0, dense Granite architecture, likely to
   load in LLMUnity. It is below the 4B in size, so it competes on latency more than on accuracy.
   **Ruled out if** it does no better than `granite4.1:3b` with thinking off, which would leave
   4.1 as the Granite to keep.

**Recommendation for the first arm:** `gemma4:e4b-it-qat` against `qwen3.5:4b`, with Bolt E4B
straight after it in the same session. Run together, the two arms say whether Gemma's E4B can
choose as well as the 4B, and whether NVIDIA's tuning improves on it.

### 4B-class candidate table

Benchmarks are makers' or third parties' claims and are not comparable across rows: harnesses,
modes and BFCL versions differ, and the tables come from companies with a model on the list.
"Pulled" means already on the RTX 5080 server.

| Model | Maker, released | Params | License; can a build bundle it? | Ollama tag, size | GGUF; LLMUnity v3.0.3 | Thinking off? | Claimed benchmarks (source) | Resident, speed (source) | Pulled |
|---|---|---|---|---|---|---|---|---|---|
| **Qwen3.5-4B** (the control) | Alibaba Qwen, Mar 2026 | 4B + vision | Apache-2.0, yes | `qwen3.5:4b`, 3.3 GB | yes; its architecture is the one b8209 names | On by default; `think: false` | IFEval 89.8, IFBench 59.2, BFCL-V4 50.3, mode not stated [Q2] | 3.1 GB, 0.46 s a decision on the 5080 (ours: findings, 11 November) | yes |
| Gemma 4 E4B QAT | Google DeepMind, 2 Apr 2026 | 4.5B effective, 8B with embeddings | Apache-2.0, yes | `gemma4:e4b-it-qat`, 6.1 GB download | yes; Gemma 4 post-dates b8209 | Off unless `<\|think\|>` | IFBench 44 [E1]; IFEval 90.02 per Liquid [L1], 44.4 per OpenBMB [M1], 45.3 per SparkLLM [Sp4] | Unpublished; about 2.5–3.5 GB by the E2B's ratio, *est.* | no (`e2b-it-qat` is) |
| Nemotron 3.5 Bolt E4B | NVIDIA, 23 Oct 2026 | 8B total, 4B effective; Gemma 4 E4B | OpenMDW-1.1 (Gemma's Apache-2.0 beneath); yes, keeping the notices | `nemotron-3.5-bolt:e4b`, 7.2–7.9 GB with projector and draft; `hf.co/nvidia/NVIDIA-Nemotron-3.5-Bolt-E4B-4GB:Q4_K_M`, 6.18 GB | yes; Gemma 4 post-dates b8209 | On by default; `enable_thinking: false` | Non-reasoning: RoleBench 67.05 (Qwen3.5-4B 53.66), EQ-Bench v3 39.40 (32.92). Reasoning: IFBench 63.79 (58.91), BFCL-v3 68.51 (70.95), Tau2 55.10 (75.06) [B1] | 3.5 GB peak, 133 tok/s, RTX 5070, Q4_K_M, NVIDIA's figures [B4] | no |
| Spark-X2.6-4B | SparkLLM, 21 Oct 2026 | 4.11B | Apache-2.0, yes | `SparkLLM/Spark-X2.6-4B:q4_k_m`, 3.5 GB; `q8_0` 4.4 GB (user namespace) | community GGUFs; needs llama.cpp b10828+, so **no** | On by default; `enable_thinking: false` (SGLang, vLLM) | Thinking high: IFEval 93.6, IFBench 75.8, BFCL-V4 71.8 [Sp4] | Unpublished; about 215 tok/s on an RTX 4090 at Q4_K_M, a third party's reading of the maker's chart [Sp4] | no |
| Granite 4.2 3B | IBM, 25 Aug 2026 | 3B dense | Apache-2.0, yes | `granite4.2:3b`, 2.2 GB | yes, likely | On by default; `enable_thinking=False` leaves an empty block | BFCL v4 52.41, IFBench 74.33, mode not stated [G2]; BFCL v4 50.8 in high reasoning per IFM [K1] | Unpublished | no |
| K2-Horizon-3.7B | IFM, card updated 1 Oct 2026 | 3.7B core (5B in the safetensors) | Apache-2.0, yes | none found | community GGUFs | **No**: `reasoning_effort` high, medium or low | High reasoning: IFEval 83.6 (loose), IFBench 47.7, BFCL v4 64.6 [K1] | Unpublished | no |
| Nanbeige4.2-3B | Nanbeige, Jul 2026 | 3B non-embedding, 4B total, looped | Apache-2.0, yes | its own Ollama fork only | its own llama.cpp fork only, so **no** | `enable_thinking=False` | All in thinking mode: IFBench 54.6 [Na1] | Unpublished | no |
| G9v3-3B | AI9Stars, Jul 2026 (third party) | 3B dense, `LlamaForCausalLM` | Apache-2.0, yes | none found in the library | community GGUFs; Llama architecture, likely | `enable_thinking=False` | None on its card; BFCL v4 47.9 in IFM's table [K1] | about 2.2 GB at Q4_K_M, a third party's estimate [G9] | no |

Column notes:
- **Logprobs.** Nobody reports anything model-specific about Ollama's `logprobs` for any of
  these. "From before the mask" is still checked on Qwen only, so each needs the scarecrow check
  before its guarded results mean anything.
- **JSON-schema output.** No maker or third party reports how any of them behaves under a
  constrained `format`. What we know is ours: the Gemma 4 family kept the framework's field order
  on Ollama 0.40.14 in both sweeps (findings, 9 November). Spark's template, served from a user
  namespace, is unknown, so the field-order pre-check matters most there. An Ollama issue reports
  `format` being ignored only when thinking was disabled, for `qwen3.5` on Ollama 0.17.6
  ([#14645](https://github.com/ollama/ollama/issues/14645)); our 0.40.14 runs show no such thing on
  Qwen, but a model new to the server is the place it would show.

### Ruled out of the 4B class, one line each

- **K2-Horizon-3.7B**: reasoning effort goes down to "low" and its card documents no way to turn
  it off; every number is in high reasoning.
- **Nanbeige4.2-3B**: a looped transformer that needs Nanbeige's own forks of llama.cpp and
  Ollama, and every number is in thinking mode. Nanbeige4.1-3B is superseded by it.
- **G9v3-3B**: Apache-2.0, a Llama architecture and a thinking switch, but no instruction-following
  or tool numbers of its own, and below Qwen3.5-4B in IFM's BFCL table. Reserve, if a 3B with a
  plain architecture is wanted for Phase 6b.
- **Nemotron 3.5 Bolt 74B-A9B**: 22 to 25 GB.
- **Spark-X2.6-1.7B, the six LFM3 models, MiniCPM5-1B**: all 2B class or smaller; there is no
  3B or 4B LFM3.
- **Community role-play finetunes of Qwen3.5-4B** (for example MiniFantasy, EmergentRP): no
  benchmarks, unknown training data, some built on refusal-removed bases. A finetune of our own is
  the "fine-tuning" section's idea, not this list's.
- **`gemma4:e4b-it-q4_K_M` and `q8_0`** (6.6 and 9.0 GB): the QAT tag is smaller and is the
  variant whose E2B we measured.
- **`qwen3.5:4b` at Q8_0** (5.2 GB): above the 4 GB budget, and the same model; a quantisation
  A/B, not a candidate.
- **Already measured, and below `qwen3.5:4b` on both sets**: `nemotron-3-nano:4b`, `granite4.1:3b`,
  `qwen3:4b-instruct-2507`, `ministral-3:3b`, `phi4-mini`, `llama3.2:3b`, `qwen3:8b` (findings,
  9 November).
- **Not released**, searched on 11 November 2026: Qwen 4 and any small Qwen 3.6 or 3.8, Phi-5,
  Ministral 4, Gemma 4.1, MiniCPM5-4B, SmolLM4, Falcon-H2.

### What this section could not verify

- How any candidate scores with thinking off on instruction following or tool calling. Only
  NVIDIA labels modes per row, and its instruction-following and tool rows are in reasoning mode.
- The resident size of `gemma4:e4b-it-qat`; the estimate is arithmetic from the E2B.
- Whether Ollama 0.40.14 loads `nemotron-3.5-bolt:e4b` (its tags carry a `dflash` draft), and
  whether `think: false` turns off Bolt's or Spark's reasoning through Ollama.
- Whether the Ollama library's Bolt tag takes image input; the Hugging Face card says text only.
- Spark-X2.6's benchmark table, which is published only as an image; its numbers here are a third
  party's transcription [Sp4].
- Whether OpenMDW-1.1 covers every file in NVIDIA's repository; OSI reviewers raised that question
  [OM].
- Release dates given only by third parties (G9v3-3B), and K2-Horizon's, which its card does not
  state.
- Any independent evaluation of Bolt E4B or Spark-X2.6; none turned up.

## High-end GPU tiers: 12 GB, 16 GB, 24 GB and up

For players with more VRAM than the default assumes, so the framework can offer a "high-end
GPU" option beside the 8 GB default. In the framework, a tier is just another provider config
asset (DR-013) naming a different model; which one a game picks for a player's card is the
game's decision. The job and its rules are unchanged: thinking off, token probabilities from
before the mask, a license that lets a build bundle the weights. Three questions are new: how
much longer a decision takes, whether that is worth it, and whether the model can ship
in-process at all.

Covered: dense models of about 8B to 31B, and mixture-of-experts (MoE) models of 8B to 35B
total with 1.5B to 4B active, which read only their active experts' weights per token.

### The VRAM budget each tier assumes

A planning assumption, **not measured**: the model file, plus about 1 GB for the KV cache at
4096 tokens and compute buffers, plus 3 to 4 GB for the game's renderer. Appendix A's 4.2 GB was
a 2.7 GB model and a *minimal* scene; a real game takes more. That leaves, for the model file:

| Tier | Cards (examples) | Model file up to about |
|---|---|---|
| 12 GB | RTX 3060 12 GB, 4070, 5070 | 7 GB |
| 16 GB | RTX 4060 Ti 16 GB, 4080, 5080 | 10–11 GB |
| 24 GB | RTX 3090, 4090 | 18 GB |
| 32 GB | RTX 5090 | 25 GB |

Ollama's tags often bundle a vision projector, which a text-only GGUF does not: `qwen3.6:35b-a3b-q4_K_M`
is 24 GB on Ollama, while Unsloth's text GGUF of the same model at UD-Q4_K_XL is about 18.3 GiB
([llama.cpp #19890](https://github.com/ggml-org/llama.cpp/discussions/19890)). For the larger
models the GGUF, pulled through `hf.co/...`, is often the one that fits.

### Latency: what a bigger model costs per decision

A decision writes about 35 tokens and reads about 240 (findings, 25 October). Prompt processing
on these cards runs at thousands of tokens a second (3,800 to 30,000 in the 5090 table below), so
it adds well under 0.1 s for every model here. **Writing the 35 tokens is the cost.**

Published generation speeds, all **third-party measurements, not ours**, and the time 35 tokens
would take at that speed (arithmetic, not a measurement):

| Model | Quant | Card | Generation (tok/s) | 35 tokens take | Source |
|---|---|---|---|---|---|
| LFM2.5-8B-A1B (1.5B active) | Q8_0 | RTX 5090 | 563 | 0.06 s | [W] |
| Nemotron 3 Nano 30B-A3B | Q4_K_M | RTX 5090 | 364 | 0.10 s | [W] |
| gpt-oss-20b (3.6B active; ruled out, for scale) | Q4_K_M | RTX 5090 | 368 | 0.10 s | [W] |
| Qwen3.6-35B-A3B | UD-Q4_K_M | RTX 5090 | 271 | 0.13 s | [W] |
| Qwen3.5-35B-A3B | UD-Q4_K_XL | RTX 5090 | 194 | 0.18 s | [LC] |
| Gemma 4 12B | Q6_K | RTX 5090 | 122 | 0.29 s | [W] |
| Qwen3.6-27B (dense) | Q4_K_M | RTX 5090 | 77 | 0.45 s | [W] |
| Gemma 4 31B (dense) | Q4_0 | RTX 5090 | 76 | 0.46 s | [W] |
| Qwen3.5-27B (dense) | Q4_K_M | RTX 4090 | ~48, *an estimate* | ~0.7 s | [WR] |
| Qwen3.5-9B (dense) | Q4_K_M | RTX 4060 Laptop | 37.7 | 0.93 s | [NY] |

**Against the 2B.** No published generation speed for `qwen3.5:2b` on these cards turned up. Our
own anchor is a whole decision, not just generation: 0.23 to 0.56 s per decision on an RTX 5080
(findings, 25 October). Generation is limited by memory bandwidth, so a rough guide is the weight
read per token compared with the 2B at Q8_0 (about 2.1 GB). This is arithmetic, not a
measurement:

| Weights read per token, against the 2B | Models |
|---|---|
| About the same (0.8–1.1×) | Every small-active MoE here: LFM2.5-8B-A1B, Qwen3-30B-A3B, Qwen3.6-35B-A3B, Nemotron 3 Nano 30B-A3B, Gemma 4 26B-A4B |
| 2–4× | Granite 4.1 8B (2.3×), Qwen3.5-9B (2.6×), Gemma 4 12B (3.4×), Ministral 3 14B (3.9×) at Q4 |
| 7–9× | Mistral Small 3.2 24B (6.9×), Qwen3.5/3.6-27B (7.7×), Granite 4.1 30B (8.6×), Gemma 4 31B (8.8×) at Q4 |

MoE models fall short of their bandwidth figure because of routing: in the 5090 table a 3B-active
MoE ran 3.5 to 4.7 times faster than the dense 27B, not the 8 times the bytes suggest. And the
game shares the card. A GPU-bound game competes for the same memory bandwidth, so every figure
here is a best case; by how much is unmeasured.

### Is the extra accuracy worth the wait?

- **Small-active MoE: the cost is VRAM, not time.** Per token they read about what the 2B reads,
  with 30B-class knowledge behind them. On a 24 GB or 32 GB card this is the natural high-end
  option.
- **Dense 8B to 14B: 2 to 4 times the 2B's weight per token.** On a 12 or 16 GB card, likely
  around half a second to a second a decision. Fine for a companion talking to the player; heavy
  when many agents decide.
- **Dense 24B to 31B: about 0.5 s on a 5090, about 0.7 s or more on a 4090** (from the speeds
  above). Only for one or two player-facing agents. Appendix A's serialized queue already takes
  about 21 s a round for 30 agents on the 2B.
- **What scale is likely to buy.** In the prototype, scaffolding beat scale: the 2B with guards
  scored 95% against 65% for the 4B without (Appendix A). The one residual only scale fixed was
  a wrong action on the right target ("That's an interesting sword you have."). So a bigger
  model is most likely to help with those residuals and with the quality of the `statement`,
  less with target grounding, which the guards already handle. That is a hypothesis for #145,
  not a result.
- **Choose by how many agents decide and how often**, not by the card alone.

### Phase 6b: in-process, or Ollama only?

- LLMUnity loads any GGUF its llama.cpp supports; the limit is size. A 5 to 9 GB file is a
  plausible optional download. A 16 to 19 GB file is realistically an opt-in "high-end pack", or
  Ollama only, for players who already run it. DR-001 already ships no `.gguf` in the package.
- **Architectures against LLMUnity v3.0.3 (llama.cpp b8209, March 2026):** Gemma 4 and Qwen3.6
  (both April 2026) post-date it. Qwen3-30B-A3B-2507 (2025), Granite 4.1's dense architecture,
  Ministral 3 and Nemotron 3 Nano 30B-A3B (December 2025) are older and likely to load. None of
  that is verified; the release notes name only Qwen 3.5.

### 12 GB tier shortlist (RTX 3060 12 GB, 4070, 5070)

1. **Granite 4.1 8B** (`granite4.1:8b`, 5.3 GB at Q4). No thinking mode, Apache-2.0. IBM reports
   IFEval 87.06, BFCL v3 68.27 and ArenaHard 68.98, against 37.80 ArenaHard for its 3B, which
   suggests a large step in chat quality. Plain dense architecture, likely to load in LLMUnity.
2. **Qwen3.5-9B** (`qwen3.5:9b`, 6.6 GB at Q4). The baseline's own family, so the cleanest test
   of scale alone. Qwen reports IFEval 91.5 and BFCL-V4 66.1, mode not stated. Thinking is on by
   default, so `think: false` is required. A third party measured 37.7 tok/s at Q4 on an RTX
   4060 Laptop, the lead's card class, though it does not fit there beside a game. Its
   architecture is the one LLMUnity v3.0.3 names.
3. **The 8 GB shortlist at Q8_0**, such as `qwen3:4b-instruct-2507-q8_0` (4.3 GB). The cheapest
   step up: same model, less quantization loss, small-model speed.

Reserve: **LFM2.5-8B-A1B** (`lfm2.5:8b`, 5.2 GB, 1.5B active), the fastest model in this whole
page (563 tok/s at Q8 on a 5090, third party), with Liquid's claim of IFEval 91.84. Its card
calls it a reasoning model and documents no way to turn that off, so it is in only if
`think: false` is shown to suppress the reasoning. LFM Open License (below USD 10M revenue).

### 16 GB tier shortlist (RTX 4060 Ti 16 GB, 4080, 5080)

1. **Gemma 4 12B** (`gemma4:12b-it-qat`, 7.2 GB; `12b-it-q4_K_M` 8.0 GB). Apache-2.0, thinking
   off unless asked for. Google reports Tau2 69.0 and MMLU Pro 77.2, and no IFEval. 122 tok/s at
   Q6_K on a 5090 (third party). Post-dates LLMUnity v3.0.3.
2. **Ministral 3 14B** (`ministral-3:14b`, 9.1 GB at Q4). Apache-2.0, no thinking in the
   Instruct model. Mistral reports Arena Hard 0.551 and WildBench 68.5, against 0.427 and 65.1 for
   Qwen3 14B non-thinking in its own table. No instruction-following or tool numbers.
3. **The 12 GB picks at Q8_0**: Granite 4.1 8B (about 8.7 GB, *est.*) or Qwen3.5-9B (10 GB).

Ruled out here: **gpt-oss-20b** fits (about 14 GB at MXFP4) and is fast, but its reasoning only
goes down to "low" and cannot be turned off. **Mistral Small 3.2 24B** at 15 GB leaves no room
for a game.

### 24 GB and up shortlist (RTX 3090, 4090; RTX 5090 at 32 GB)

1. **Qwen3-30B-A3B-Instruct-2507** (`qwen3:30b-a3b-instruct-2507-q4_K_M`, 19 GB). Non-thinking
   only, Apache-2.0, 3.3B active. Qwen reports IFEval 84.7, BFCL-v3 65.1, Creative Writing v3 86.0
   and WritingBench 85.5, the best writing claims on this page. Tight on 24 GB, comfortable on
   32 GB. Its architecture has been in llama.cpp since 2025, so it is the likeliest of these to
   load in LLMUnity.
2. **Gemma 4 26B-A4B** (`gemma4:26b-a4b-it-qat`, 16 GB). The one that leaves real room on a
   24 GB card. Apache-2.0, thinking off unless asked for, 3.8B active. Google reports Tau2 68.2
   and MMLU Pro 82.6; Liquid's table gives it IFEval 91.40. Needs a newer LLMUnity.
3. **Qwen3.6-35B-A3B** (Unsloth's text GGUF, about 18.3 GiB at UD-Q4_K_XL; Ollama's
   `qwen3.6:35b-a3b-q4_K_M` is 24 GB, too big for a 24 GB card). The newest Qwen, Apache-2.0,
   thinking on by default. Its card lists no IFEval or BFCL; the Qwen3.5-35B-A3B card claims
   IFEval 91.9 and BFCL-V4 67.3. 271 tok/s on a 5090 (third party). The 32 GB pick.
4. **Qwen3.6-27B** (`qwen3.6:27b-q4_K_M`, 17 GB), or Qwen3.5-27B. The quality ceiling, dense:
   Qwen3.5-27B claims IFEval 95.0 and BFCL-V4 68.5, mode not stated. 77 tok/s on a 5090, so
   about 0.45 s to write a decision. For one or two player-facing agents.

Reserves: **Nemotron 3 Nano 30B-A3B**, the fastest of these (364 tok/s on a 5090), but Ollama's
Q4 tag is 24 GB, its license is NVIDIA's, and no reasoning-off numbers are published. **Granite
4.1 30B**, dense and with no thinking mode, with IBM's IFEval 89.65 and BFCL v3 73.68, at dense
30B speed. **Mistral Small 3.2 24B** (15 GB, no thinking, Apache-2.0, no IFEval published).
**Gemma 4 31B** (19 GB at QAT, 76 tok/s on a 5090).

### High-end candidate table

All benchmarks are makers' claims unless marked. Sizes are Ollama's unless marked.

| Model | Maker, released | Total / active | License | Ollama tag, size | Thinking off? | Claimed benchmarks (source) |
|---|---|---|---|---|---|---|
| Granite 4.1 8B | IBM, 29 Apr 2026 | 8B dense | Apache-2.0 | `granite4.1:8b`, 5.3 GB | No thinking mode | IFEval 87.06, BFCL v3 68.27, ArenaHard 68.98 [G3] |
| Qwen3.5-9B | Qwen, Mar 2026 | 9B dense | Apache-2.0 | `qwen3.5:9b`, 6.6 GB Q4 / 10 GB Q8 | On by default; `think: false` | IFEval 91.5, BFCL-V4 66.1, mode not stated [Q2] |
| LFM2.5-8B-A1B | Liquid AI, 2026 | 8.3B / 1.5B | LFM Open v1.0 | `lfm2.5:8b`, 5.2 GB | Reasoning model; no off switch documented | IFEval 91.84, BFCLv3 64.79 [L3] |
| Ministral 3 8B | Mistral AI, Dec 2025 | 8B + vision | Apache-2.0 | `ministral-3:8b`, 6.0 GB | No thinking in Instruct | Arena Hard 0.509, WildBench 66.8 [Mi2] |
| Gemma 4 12B | Google, 2026 | 11.95B dense | Apache-2.0 | `gemma4:12b-it-qat`, 7.2 GB | Off unless `<\|think\|>` | Tau2 69.0, MMLU Pro 77.2 [E2] |
| Ministral 3 14B | Mistral AI, Dec 2025 | 13.5B + 0.4B vision | Apache-2.0 | `ministral-3:14b`, 9.1 GB | No thinking in Instruct | Arena Hard 0.551, WildBench 68.5 [Mi2] |
| gpt-oss-20b | OpenAI | 21B / 3.6B | Apache-2.0 | MXFP4, about 14–16 GB | **No**: low, medium or high only | — [O1] |
| Mistral Small 3.2 24B | Mistral AI, 2025 | 24B dense | Apache-2.0 | `mistral-small3.2:24b`, 15 GB | No thinking mode | Arena Hard v2 43.1, WildBench v2 65.33, internal IF 84.78 [Mi3] |
| Gemma 4 26B-A4B | Google, 2 Apr 2026 | 25.2B / 3.8B | Apache-2.0 | `gemma4:26b-a4b-it-qat`, 16 GB | Off unless `<\|think\|>` | Tau2 68.2, MMLU Pro 82.6 [E2]; IFEval 91.40 per Liquid's table [L3] |
| Qwen3-30B-A3B-Instruct-2507 | Qwen, 2025 | 30.5B / 3.3B | Apache-2.0 | `qwen3:30b-a3b-instruct-2507-q4_K_M`, 19 GB | No thinking mode | IFEval 84.7, BFCL-v3 65.1, Creative Writing v3 86.0, WritingBench 85.5 [Q4] |
| Nemotron 3 Nano 30B-A3B | NVIDIA, 15 Dec 2025 | 30B / 3.5B | NVIDIA Nemotron Open Model License | `nemotron-3-nano:30b-a3b-q4_K_M`, 24 GB | On by default; `enable_thinking=False` | IFBench 71.5, BFCL v4 53.8, mode not labelled [N2] |
| Qwen3.5-27B / Qwen3.6-27B | Qwen, Feb / Apr 2026 | 27B dense | Apache-2.0 | `qwen3.6:27b-q4_K_M`, 17 GB | On by default; `think: false` | 3.5-27B: IFEval 95.0, BFCL-V4 68.5, mode not stated [Q5] |
| Granite 4.1 30B | IBM, 29 Apr 2026 | 30B dense | Apache-2.0 | `granite4.1:30b`, 17 GB | No thinking mode | IFEval 89.65, BFCL v3 73.68 [G3] |
| Gemma 4 31B | Google, 2 Apr 2026 | 30.7B dense | Apache-2.0 | `gemma4:31b-it-qat`, 19 GB | Off unless `<\|think\|>` | Tau2 76.9, MMLU Pro 85.2 [E2] |
| Qwen3.5-35B-A3B / Qwen3.6-35B-A3B | Qwen, Feb / Apr 2026 | 35B / 3B | Apache-2.0 | `qwen3.6:35b-a3b-q4_K_M`, 24 GB; text GGUF about 18.3 GiB [LC] | On by default; `think: false` | 3.5-35B-A3B: IFEval 91.9, BFCL-V4 67.3, mode not stated [Q5] |

### Testing the high-end tiers

- The team's RTX 5080 desktop (16 GB, [`environment.md`](environment.md)) can run the 12 and 16
  GB tiers. For the 12 GB tier that tests accuracy and speed on a faster card, not whether the
  model fits a 12 GB card beside a game.
- **No team machine with 24 GB or more is listed**, so that tier cannot be A/B-tested until one
  is found.
- Each run has the shape of the 8 GB rows below: `qwen3.5:2b` as the control on the same machine
  and session, one model resident at a time (hard rule 3). Latency from the 5080 does not
  transfer to other cards (`environment.md`).

## Another route: fine-tuning the 2B (an idea, noted 11 November 2026)

**Not planned, not started, nothing here measured.** The lead asked whether `qwen3.5:2b` could be
fine-tuned for our task in place of swapping it for a bigger model. This section records the
thinking so it is not redone.

**Why it is tempting.** On the RTX 5080 the 2B scores 263 of 389 on the interim set against the
4B's 330, holds on a third of its turns in GreyBoxStrategy, and copies its statement rule into
its statements (findings, 9 and 11 November). A 2B-class model is small enough to train a LoRA
adapter for on a 16 GB card, and it is the size that fits an 8 GB card beside a game.

**Why it waits.**

- **The framework's promise is that it works with the model a developer has.** A model tuned on
  our demos' actions helps our demos. To help a developer with their own actions it would have to
  learn the general task (choose one action and target from a list, given facts), which needs
  training data from many different games, not from ours alone.
- **The data is the real work.** It needs thousands of labelled decisions that are not evaluation
  prompts: training on a prompt and then scoring on it invalidates the score, as hard rule 4 says
  of few-shot examples. The cheapest source is a larger model's answers on generated game states,
  which GreyBoxStrategy can produce in bulk with their legal moves.
- **The gain is small where we measure today.** On the RTX 5080 the 4B takes 0.46 s a decision
  against the 2B's 0.35 s, and 3.1 GB against 2.4 GB. A tuned 2B pays off mainly on a laptop and
  on small cards.
- **Cheaper fixes come first.** Prompt wording is still moving the 2B by several points a change
  (#151, #244).
- **Shipping it adds work.** The adapter has to load in Ollama and later in LLMUnity (Phase 6b),
  and the base model's license has to allow distributing a tuned copy. Neither was checked.

**The smallest honest experiment, if someone takes it.** Generate a few thousand GreyBoxStrategy
decisions, label them with the 4B, train an adapter on the 2B, and score it on the interim set,
which it never saw, against the untuned 2B in the same session on the same machine. One or two
days of work. A negative result goes in [`findings.md`](findings.md) like any other.

## How this feeds Phase 4

- **#151's "`qwen3.5:2b` vs `qwen3.5:4b`" row stays first.** It is the prototype's headline
  on the framework and needs only #145's sequential-arms mode. Each shortlisted model is then
  one more row of the same shape: `qwen3.5:2b` as the control, the candidate as the treatment,
  on the same prompt set, machine and session, one model resident at a time (hard rule 3), on
  AC power, temperature 0, `think: false`. The arms differ by model, so "same model" in hard
  rule 2 becomes "same everything else". Record each arm's exact tag and digest.
- **Before a candidate's first run**, four cheap checks, none of them an accuracy claim:
  that `think: false` really yields no reasoning tokens (`eval_count` against the reply's
  length); the scarecrow logprobs check; VRAM from `/api/ps` with `size_vram` equal to
  `size`; and that its raw replies keep the schema's field order, `action`, `target`,
  `statement`, which `Qwen3-4B-Instruct-2507` did not on Ollama 0.40.14 (findings, 5 November
  2026). Ollama sorts the schema's keys for a model it serves through the GGUF chat template;
  the server log's "template selection" line shows which (`gguf_chat_template` loses the order,
  and `MiniCPM5-2B` logged it too). Setting `OLLAMA_GO_TEMPLATE=1` on the server moves both onto
  `go_template`, which keeps the order, but also formats the prompt with another template, so say so
  with any result taken that way (findings, 9 November 2026). `AGENERELA_OLLAMA_MODEL` already lets the Phase 3 live suite run another model, as a
  20-prompt smoke test.
- **The grounding threshold is per model until shown otherwise.** 0.8 was set on Qwen's
  tokenizer. Another tokenizer splits target ids differently, so a candidate that loses only
  through refusals of good requests may need its own calibration run, which #151 already owes
  for Qwen.
- **Compare latency on tokens, not seconds alone.** Tokenizers differ, so the same prompt is a
  different number of tokens per model; record `prompt_eval_count` and `eval_count` beside the
  time.
- **Tim's Evaluation area (#121)** owns trying out models; this page is its starting list.
  When a candidate is measured, its result goes in [`findings.md`](findings.md), and this page
  gets a line pointing there rather than a copy of the number.
- **For Phase 6b**, the winner's GGUF, quantization and license go in this wiki, as the build
  plan asks. Recheck the license on the model card on the day a build bundles it.

## What this page could not verify

- Whether each maker's numbers are thinking-on or thinking-off, where the card does not say
  (Qwen3.5-4B, MiniCPM5-2B, Granite 4.2, LFM3). Only Qwen's 2B table and NVIDIA's split them.
- How to turn MiniCPM5-2B's thinking off; its card shows only `enable_thinking=True`.
- Whether LLMUnity v3.0.3's llama.cpp (b8209) loads LFM3, Gemma 4 or Nemotron-H GGUFs. Its notes
  name only Qwen 3.5.
- Whether Ollama's `logprobs` are from before the mask for any model but `qwen3.5:2b` and, since
  5 November 2026, `Qwen3-4B-Instruct-2507` (findings, "First model A/B").
- Release dates that only third parties give (MiniCPM5-2B, LFM2.5-2.6B, LFM3-1.7B), and whether
  all six LFM3 models carry the LFM license (one third-party table lists three as Apache-2.0).
- Sizes marked *est.*, which are arithmetic, not downloads.
- **High-end tiers:** the VRAM a game's renderer needs (the tier budgets assume 3 to 4 GB); any
  published generation speed for `qwen3.5:2b` on the same cards as the bigger models, so the
  "against the 2B" column is arithmetic; how much a GPU-bound game slows generation; whether
  LFM2.5-8B-A1B's reasoning can be turned off; the IFEval and BFCL numbers for Qwen3.6, whose
  cards list none; and the release dates of Qwen3-30B-A3B-2507, Gemma 4 12B and LFM2.5-8B-A1B.

## Sources

All accessed 4 November 2026. Maker pages are the makers' claims about their own models; the
tables in [M1] and [L1] are also makers' claims, about competitors.

- [Q1] Qwen3.5-2B model card — https://huggingface.co/Qwen/Qwen3.5-2B
- [Q2] Qwen3.5-4B model card — https://huggingface.co/Qwen/Qwen3.5-4B
- [Q3] Qwen3-4B-Instruct-2507 model card — https://huggingface.co/Qwen/Qwen3-4B-Instruct-2507
- [G1] Granite 4.1 3B model card — https://huggingface.co/ibm-granite/granite-4.1-3b
- [G2] Granite 4.2 3B model card — https://huggingface.co/ibm-granite/granite-4.2-3b
- [N1] NVIDIA Nemotron 3 Nano 4B model card — https://huggingface.co/nvidia/NVIDIA-Nemotron-3-Nano-4B-BF16;
  license — https://www.nvidia.com/en-us/agreements/enterprise-software/nvidia-nemotron-open-model-license
- [M1] MiniCPM5-2B model card, with its comparison table — https://huggingface.co/openbmb/MiniCPM5-2B
- [M2] Third-party reviews of MiniCPM5-2B — https://www.eesel.ai/blog/minicpm5-2b,
  https://pasqualepillitteri.it/en/news/17133/minicpm5-2b-open-source-beats-qwen35-4b
- [L1] LFM3-1.7B model card, with its comparison table — https://huggingface.co/LiquidAI/LFM3-1.7B;
  release coverage — https://ai-tldr.dev/releases/liquid-lfm3/
- [L2] LFM2.5-2.6B model card — https://huggingface.co/LiquidAI/LFM2.5-2.6B; LFM license —
  https://www.liquid.ai/lfm-license
- [E1] Gemma 4 E4B model card (E2B and E4B rows) — https://huggingface.co/google/gemma-4-E4B-it;
  Ollama page — https://ollama.com/library/gemma4
- [Mi1] Ministral 3 3B Instruct 2512 model card — https://huggingface.co/mistralai/Ministral-3-3B-Instruct-2512
- [S1] SmolLM3-3B model card — https://huggingface.co/HuggingFaceTB/SmolLM3-3B
- [P1] Phi-4-mini-instruct model card — https://huggingface.co/microsoft/Phi-4-mini-instruct
- [Ll1] Llama 3.2 3B Instruct model card — https://huggingface.co/meta-llama/Llama-3.2-3B-Instruct
- Ollama library tag pages: https://ollama.com/library/qwen3.5/tags, https://ollama.com/library/qwen3/tags,
  https://ollama.com/library/gemma4/tags, https://ollama.com/library/granite4.1,
  https://ollama.com/library/granite4.2, https://ollama.com/library/nemotron-3-nano,
  https://ollama.com/library/lfm3, https://ollama.com/library/lfm2.5,
  https://ollama.com/library/ministral-3/tags
- LLMUnity releases — https://github.com/undreamai/LLMUnity/releases
- Ollama logprobs issues — https://github.com/ollama/ollama/issues/13638,
  https://github.com/ollama/ollama/issues/16117

High-end tiers:

- [G3] Granite 4.1 30B model card (3B, 8B and 30B rows) — https://huggingface.co/ibm-granite/granite-4.1-30b
- [Q4] Qwen3-30B-A3B-Instruct-2507 model card — https://huggingface.co/Qwen/Qwen3-30B-A3B-Instruct-2507
- [Q5] Qwen3.5-35B-A3B model card (27B and 35B-A3B columns) — https://huggingface.co/Qwen/Qwen3.5-35B-A3B;
  Qwen3.6-35B-A3B model card — https://huggingface.co/Qwen/Qwen3.6-35B-A3B
- [E2] Gemma 4 26B-A4B model card (12B, 26B-A4B and 31B rows) — https://huggingface.co/google/gemma-4-26B-A4B-it
- [Mi2] Ministral 3 14B Instruct 2512 model card (3B, 8B and 14B rows) — https://huggingface.co/mistralai/Ministral-3-14B-Instruct-2512
- [Mi3] Mistral Small 3.2 24B Instruct 2506 model card — https://huggingface.co/mistralai/Mistral-Small-3.2-24B-Instruct-2506
- [N2] Nemotron 3 Nano 30B-A3B model card — https://huggingface.co/nvidia/NVIDIA-Nemotron-3-Nano-30B-A3B-BF16
- [L3] LFM2.5-8B-A1B model card, with its comparison table — https://huggingface.co/LiquidAI/LFM2.5-8B-A1B
- [O1] gpt-oss-20b model card — https://huggingface.co/openai/gpt-oss-20b
- [W] RTX 5090 llama-bench results (third party, witcheer, llama.cpp, all layers on the GPU,
  tg128) — https://huggingface.co/datasets/witcheer/rtx-5090-benchmarks
- [LC] Qwen3.5-35B-A3B on an RTX 5090, llama-bench (third party) — https://github.com/ggml-org/llama.cpp/discussions/19890
- [NY] Qwen3.5-9B on an RTX 4060 Laptop, llama-bench (third party) — https://www.nidhiyashwanth.com/blog/local-ai-benchmark
- [WR] Qwen3.5-27B on an RTX 4090, an estimate, not a measurement (third party) — https://willitrunai.com/can-run/qwen-3.5-27b-on-rtx-4090-24gb
- Ollama tag pages: https://ollama.com/library/qwen3.6/tags, https://ollama.com/library/mistral-small3.2/tags,
  https://ollama.com/library/nemotron-3-nano/tags

4B-class section, all accessed 11 November 2026. [Q2] and [E1] above were re-read the same
day; [E1]'s IFBench 44 is from the card's evaluation sidebar. [G2], [L1] and [M1] are cited as
read on 4 November.

- [B1] Nemotron 3.5 Bolt E4B model card (BF16), with its comparison against Qwen3.5-4B — https://huggingface.co/nvidia/Nemotron-3.5-Bolt-E4B-BF16
- [B2] Nemotron 3.5 Bolt E4B Q4_K_M GGUF card, with its quantised comparison — https://huggingface.co/nvidia/NVIDIA-Nemotron-3.5-Bolt-E4B-4GB
- [B3] Ollama library page and tags — https://ollama.com/library/nemotron-3.5-bolt:e4b,
  https://ollama.com/library/nemotron-3.5-bolt/tags
- [B4] NVIDIA's memory and speed figures for Bolt E4B, as reported by a third party (Magica) —
  https://magica.com/news/nvidia-nemotron-3-5-bolt-e4b-local-agents
- [OM] OpenMDW-1.1: Linux Foundation release — https://www.linuxfoundation.org/press/linux-foundation-releases-openmdw-1.1-nvidia-adopts-openmdw-for-cosmos-isaac-gr00t-ising-and-nemotron-ai-model-families;
  OSI license-review thread, August 2026 — https://lists.opensource.org/pipermail/license-review_lists.opensource.org/2026-August/006125.html;
  license text — https://github.com/OpenMDW/openmdw
- [Sp1] Spark-X2.6 README (release dates, architecture, llama.cpp and Ollama requirements) — https://github.com/XHToken/Spark-X2.6
- [Sp2] Spark-X2.6-4B model card — https://huggingface.co/XHToken/Spark-X2.6-4B
- [Sp3] Spark-X2.6-4B on Ollama (user namespace) — https://ollama.com/SparkLLM/Spark-X2.6-4B
- [Sp4] A third party's transcription of the card's benchmark images, and its chart readings
  (mer.vin) — https://mer.vin/news/spark-x2-6-4b-4b-on-device-agent-model-benchmarks-and-local-setup/
- [K1] K2-Horizon-3.7B model card, with its comparison table — https://huggingface.co/IFM/K2-Horizon-3.7B
- [Na1] Nanbeige4.2-3B model card — https://huggingface.co/Nanbeige/Nanbeige4.2-3B
- [G9] G9v3-3B model card — https://huggingface.co/AI9Stars/G9v3-3B; its size estimate, a third
  party's — https://llmrun.dev/model/ai9stars-g9v3-3b
- Ollama tag page: https://ollama.com/library/gemma4/tags
- For finding models only, not cited for any number: Artificial Analysis's list of tiny open
  models, an aggregator — https://artificialanalysis.ai/models/open-source/tiny
