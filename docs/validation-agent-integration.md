# Validation: proving an agent uses Coop rather than guessing

A specification for the test that decides whether Coop is integrated with Microsoft Agent Framework or merely adjacent to it. No code yet. This document states what must be observed, how the observation is made, and what would make a passing run meaningless.

## The question this answers

Coop has two sides that can each be tested alone. `Coop.Library.Execution` compiles and runs C# and is proven by 20 tests. `Coop.Services.Agent` registers a `run_csharp` tool with an agent framework provider and is proven by 11 tests. Both suites pass.

That proves neither side is wired to the other. A tool can be registered, described correctly, and never chosen. An execution engine can be perfect and never called. The failure this validation exists to catch is a run that looks green on both suites and contains no moment where an agent decided to compile something.

**The property to prove:** given a task whose answer cannot be obtained by reading, an agent running under the agent framework invokes `run_csharp`, and the outcome of that invocation changes what the agent does next.

## The central rule

A passing run must contain evidence that the model's output was different because Coop returned a result. Anything weaker tests the framework's plumbing, not the integration.

| Test shape | What it proves | Verdict |
| --- | --- | --- |
| Call the `AIFunction` directly | Coop executes | Already covered by unit tests, not this validation |
| Assert the tool is registered and described | Registration works | Necessary, insufficient on its own |
| Agent answers a question that needs a model but not execution | Framework runs | Irrelevant |
| Agent asked a question only answerable by running code, and the answer contains the run's actual output | Integration holds | This validation |

The last row is the target. The discriminator is that the correct answer is **not derivable from the prompt**. If the model could produce it by reasoning, a run where Coop was never called still passes.

## Fixed point of the test

One task, one prompt, one expected observation.

The task must have these properties:

1. **The answer is a fact about a specific runtime**, not a fact about C#. A question answered by language knowledge lets the model skip execution.
2. **The answer is short and exact**, so the assertion is equality rather than judgement.
3. **The answer changes when the environment changes**, so a memorised or guessed answer is detectable across runs.

The recommended shape: ask the agent to determine something that depends on the machine it is running on, obtained by executing a few lines of C#, for example a computed value that depends on the current runtime's behaviour rather than on a constant.

The prompt must not name `run_csharp`, must not mention compiling, running, or Coop. Naming the tool tests instruction-following. The skill exists to make the model choose execution when it is needed, and the test must therefore leave the choice to the model.

## What must be observed

Each observation is a recorded fact about the run, not an assertion inside the agent's reasoning.

| # | Observation | Evidence | Why it is required |
| --- | --- | --- | --- |
| 1 | The tool was offered | The framework's request shows `run_csharp` in the tool list for the turn | A tool that was not offered cannot have been chosen |
| 2 | The tool was chosen | A tool-call record naming `run_csharp` with its arguments | The integration point |
| 3 | Coop executed | A `Coop.Library.Records` record exists whose source matches the arguments, and its `executionId` appears in the tool result returned to the model | Proves the call reached the engine, not a stub |
| 4 | The result reached the model | The conversation contains a tool result whose content is the outcome JSON | A call whose result is dropped is not integration |
| 5 | The answer depends on the result | The final answer contains the value that the execution produced and could not have been inferred | The discriminator |
| 6 | The framework's own mechanisms were used | The run is a `ChatClientAgent` with the Coop context provider attached; no bespoke loop exists in Coop | Confirms Coop did not reimplement orchestration |

Observations 3 and 5 together are the proof. Without 3, nothing ran. Without 5, running changed nothing.

## The negative control

A validation without a negative control cannot distinguish "the agent chose execution" from "the agent always answers and execution happens to be harmless".

Run the same agent, same framework, same prompt shape, with the Coop context provider **not attached**. Required outcome: the agent cannot answer correctly, or answers with an explicit statement that it cannot verify. If the unconfigured agent produces the correct answer, the question is answerable without execution and the test is not measuring the integration.

Record the negative control's answer verbatim alongside the positive run. A validation that reports only the passing case has hidden the only comparison that gives it meaning.

## Cache interference, and why the second run matters

Coop caches by content. A second identical submission is served from the artefact store and returns a reuse marker.

| Run | Expected | What it would reveal if wrong |
| --- | --- | --- |
| First, cold | Executes, `reused` absent | A cache that never misses makes every subsequent claim meaningless |
| Second, identical prompt | Served from cache, `reused` present and naming the first execution | Reuse is invisible to the caller, so a cached answer is indistinguishable from a fresh one |

Run the pair. A validation that runs only the cold case proves execution and says nothing about the cache; one that runs only the warm case proves nothing at all.

## Where the run happens, and what it needs

| Requirement | Note |
| --- | --- |
| A model credential | The only secret. Never written to a file, a log, or a command line; supplied to the process environment of the run only |
| Network access to the model provider | Coop itself needs no network for an execution with no package requests |
| A writable data root | Records, artefacts and locks are written under a directory the run owns |
| .NET SDK 10 | Compilation happens in-process through Roslyn and execution through the `dotnet` host |

A run without a credential must be skipped, not passed. A skipped validation reports as skipped and never as green.

## What counts as a failure

Each of these is a failure with a distinct diagnosis, because the repair differs.

| Symptom | Diagnosis |
| --- | --- |
| Tool not in the offered list | The context provider is not attached, or the framework rejected the registration |
| Tool offered, never chosen | The tool description or the skill text does not make execution look like the way to answer. This is a prompting defect, not a code defect, and it is the expected first failure |
| Chosen, no record in the store | The call did not reach the engine: a stub, a wiring error, or an exception swallowed by a wrapper |
| Record exists, answer does not contain the value | The result did not reach the model, or the model ignored it. Inspect the conversation, not the code |
| Correct answer with no tool call | The question is answerable without execution. Replace the question; do not celebrate the pass |
| Negative control also correct | Same as above. The test measures nothing |

The second and fifth rows are the ones that will actually occur, and both are findings about the test rather than about Coop.

## Acceptance

The validation passes when all of the following are recorded in one transcript:

1. The tool list offered to the model for the decisive turn.
2. The tool call, with its arguments.
3. The record's `executionId` and the matching stored document.
4. The tool result as the model received it.
5. The final answer, containing the value only execution could produce.
6. The second run's reuse marker naming the first execution.
7. The negative control's answer, showing it could not produce the same value without the provider.

It fails if any of the seven is missing, including when the final answer happens to be correct.

## Validating without a model provider

Most of this document can be run offline, deterministically, in seconds, with no credential and no tokens. The mechanism is a scripted `IChatClient`.

Microsoft Agent Framework is built on `Microsoft.Extensions.AI`. An agent's model is an `IChatClient`, which is an interface, so the model can be replaced with a class that returns a fixed response. Wrapping that fake in `FunctionInvokingChatClient` runs the framework's **real** tool loop: the fake returns a `FunctionCallContent` for `run_csharp`, the framework invokes the actual `AIFunction`, the actual `Coop.Library.Execution` compiles and runs the actual C#, and the framework sends the real tool result back to the fake, which then produces the answer.

What that proves, with no provider involved:

| Proven | How |
| --- | --- |
| The tool contract is callable from the framework | The fake's call is dispatched by the framework's own invoker |
| The call reaches real compilation and execution | The returned JSON carries an `executionId`, `status`, `compile.success` and `run.standardOutput` from a real run |
| A compile failure is delivered as an answer rather than an exception | A broken submission returns `status: failed` with diagnostics, and the loop completes |
| The result reaches the model | The fake is asked a second time and its prompt contains the tool result |

What it does **not** prove, and must not be reported as proven:

- **That a real model chooses the tool.** The fake always chooses it. Tool choice is a property of the model and its instructions, and only a live run can observe it. This is the one question that costs tokens, and it should be asked rarely and with the cheapest available model.
- **That the description and skill text are good enough.** A scripted model reads neither.

So the split is: the offline test is the **gate**, run on every change, and it fails the moment the wiring breaks. The live test is a **sample**, run when the description, the skill text or the model changes, and its result is a rate across repeats rather than a single pass.

The profile of the offline version, measured: two tests, three seconds, no network, no credential, and it is now part of the `build-test` stage.

One further consequence worth stating. Because the fake is code, the scenario is a fixture rather than a negotiation, so the task can be the smallest thing that produces a real observation — a few lines that print one line. There is no reason for the offline scenario to model a realistic task, and every reason for it to be fast.

## Open questions a deployment must answer

| Question | Why it cannot be assumed |
| --- | --- |
| Which model, and at what temperature | A tool-choice behaviour measured at temperature zero does not transfer to a sampling configuration, and the reverse is also true |
| Whether the measurement is stable across repeats | One successful choice may be luck. A rate over several runs is stronger than a single pass and should be recorded as a rate |
| Whether a proxy or harness records the offered tool list | Observation 1 is only available if the run can see the request, which some providers do not expose. If it cannot be observed, it is undetermined and must be reported as undetermined |

The third question decides whether the full acceptance is achievable on a given deployment. Where the request is not visible, observations 3, 4 and 5 still carry the proof and the gap is stated rather than filled.
