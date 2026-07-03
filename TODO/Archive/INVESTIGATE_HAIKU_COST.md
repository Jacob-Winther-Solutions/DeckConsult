# Investigate: why is a Haiku run costing ~$0.20?

> **Context.** The agent is configured to use **Claude Haiku 4.5** ($1 / MTok
> input, $5 / MTok output). Across 20 test builds the average cost is **~$0.20
> per run**. Expected cost for a structured-extraction workload on Haiku is
> **~$0.01–0.05/run**, so we are **~5–15× over budget**. Something is sending far
> more tokens than the design implies. Find out what.
>
> **Adapt to the real codebase.** Type names, the agent entry point, and the SDK
> surface below are guesses from a planning conversation — map them to what's
> actually there. **Measure before theorizing.**

## The cost model (use this to interpret every finding)

```
run_cost = Σ_over_all_calls_in_a_run(
    input_tokens  × $1  / 1_000_000
  + output_tokens × $5  / 1_000_000
)
```

Reference points for a **single** $0.20 run:
- **Pure input** hitting $0.20 ⇒ **~200,000 input tokens** (near Haiku's 200k limit).
- **Pure output** hitting $0.20 ⇒ **~40,000 output tokens**.
- Or any mix, e.g. 100k input ($0.10) + 20k output ($0.10), or **10 calls × $0.02**.

So the excess is one (or more) of: *too many calls per run*, *huge input per
call*, *huge output per call*, or *caching not working*. The steps below isolate
which.

## Step 1 — Instrument `usage` on every call (do this first)

Log the `usage` block returned by each Messages API response. Capture, per call
and summed per run:

- `input_tokens`
- `output_tokens`
- `cache_creation_input_tokens`
- `cache_read_input_tokens`
- the **model string actually sent**
- a **call counter** scoped to one build

Run one build. Print the per-call table and the run total. This single table
usually reveals the culprit before any guessing.

## Step 2 — How many LLM calls does one build make?

The design intends the LLM as **structured extraction at a few defined points**,
**not** a free-running ReAct tool loop. Confirm reality matches:

- Count calls per build (from Step 1's counter).
- If it's more than the handful of intended consultation points, look for an
  unintended loop, a retry/reconciliation cycle that re-calls the model, or tool
  round-trips that echo the full context back each hop.
- **Amplification is the most common cause of surprise cost.** 10 modest calls
  cost the same as one huge one.

## Step 3 — Measure input size, and audit what's in the prompt

For the largest-input call, dump the assembled request and check for data that
should **not** be there:

- Full card pool / bulk Scryfall data / raw EDHREC JSON pasted into the prompt.
  The LLM emits **weights and preferences only** — it should receive a compact
  summary, not the dataset the deterministic code operates on.
- Oversized tool-definition schemas (these are billed as input **on every call**).
- The entire conversation/build history re-sent each call when only a slice is needed.
- Verbose system prompt / few-shot examples repeated uncached (see Step 5).

Rule of thumb: if any single call's `input_tokens` is in the tens of thousands,
find what's padding it and move it out of the prompt.

## Step 4 — Measure output size

Confirm the model is emitting **only** the structured weights/preferences:

- If `output_tokens` is large (thousands), the model is likely generating things
  the deterministic layer should own — card lists, long explanations, full JSON
  dumps, or restated input.
- Output is **5× the price of input** on Haiku, so bloated output hurts most.
- Tighten the tool schema / prompt so the model returns the minimal payload.

## Step 5 — Verify prompt caching is actually working

- Is caching enabled on the static prefix (system prompt, tool definitions, any
  fixed context)?
- In Step 1's data, is `cache_read_input_tokens` **> 0** on the 2nd+ calls?
  If it's always 0, caching isn't hitting — check the prefix is byte-stable
  (no per-run timestamps/IDs before the cached block) and that `cache_control`
  is set correctly.
- Working cache turns a repeated prefix from full input price into ~10% of it.

## Step 6 — Confirm the model string end-to-end

Cheap gotcha, worth ruling out: verify the model **actually sent** (from Step 1's
logged model string) is a Haiku ID and not silently Sonnet/Opus via a default,
a fallback, an env override, or the BYOK/client factory picking a different model.

## Step 7 — Reconcile against the Console

Cross-check the per-run cost computed from `usage` against the **Anthropic Console
usage dashboard** for the test window. If the Console shows *more* than your logs
account for, there are calls your instrumentation isn't capturing (background
retries, a second component also calling the API, health checks, etc.).

## Report back

Produce a short findings note with:

1. **Calls per build** (count) and, if >expected, where the extra calls originate.
2. **Per-call token table** for one representative build (input / output / cache).
3. **The single biggest token sink** identified (which call, input vs output, and why).
4. **Whether caching is hitting** (`cache_read_input_tokens` > 0 on repeat calls).
5. **Confirmed model string** actually in use.
6. A **projected post-fix cost/run** and the specific changes to get there.

Do **not** change architecture to fix this — the target is removing wasted tokens
(trim prompt inputs, cache the static prefix, minimize output, collapse redundant
calls), not redesigning the agent pipeline.
