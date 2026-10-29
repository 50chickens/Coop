# Execution

Coop accepts one C# submission, compiles it, runs it in a contained child process under a deadline, and reports a
structured outcome. This document defines what a submission carries, what an outcome carries, and every step between
them. Reuse of a previous answer is the subject of the artefact and result storage specification, serialisation of
concurrent invocations the subject of the locking specification, and package availability the subject of the package
specification.

## The submission

A submission is one source file compiled as a console application. No project file, second source file, generated code
or analyser takes part in the compilation.

| Element | Contract |
| --- | --- |
| source text | Inline C# source. When present it is what compiles; a supplied source path is then only the label diagnostics carry. |
| source path | Path to a `.cs` file Coop can read. Used when no source text is supplied. |
| parameters | Map of string to string. Required, and must be an object; send `{}` when there are none. |

Exactly one of the two source elements supplies the code. There is no named refusal for a submission that supplies
neither; reading then fails on its own before compilation. See Limitations.

Parameters are not a language-level construct. They reach the submission as the command-line arguments of its own
process, described in the template specification and repeated here because they are part of the submission contract:

| Property | Behaviour |
| --- | --- |
| form | one argument per parameter, `key=value` |
| ordering | sorted by key with an ordinal comparison, so the caller's insertion order does not survive |
| value | everything after the first `=`; may contain `=`, spaces and punctuation; never quoted, escaped or encoded |
| key | everything before the first `=`; must not be empty |
| uniqueness | a map, so each key arrives once |
| empty | a submission with no parameters receives an empty argument list |

A submission reads `string[] args` and splits each argument on its first `=`.

## The request

A request is one JSON object with nine fields. Property names are camelCase.

| Field | Type | Required | Default | Carries |
| --- | --- | --- | --- | --- |
| `name` | string | yes | none | name of the execution |
| `description` | string | yes | none | purpose or type of the execution |
| `category` | string | yes | none | category the execution is grouped under |
| `timestamp` | string | yes | see note | ISO-8601 date-time with offset |
| `submission` | object | yes | none | the code, as described above |
| `timeoutSeconds` | integer | no | `60` | the deadline asked for, clamped to the ceiling below |
| `packages` | array | no | `[]` | package entries, each `{ "id", "version" }` |
| `response` | object | no | `null` | output only; a client sends null and Coop fills it |

An absent `timestamp` deserialises to `0001-01-01T00:00:00+00:00` rather than being refused.

`shouldRunAsync` is deliberately absent from this contract. An earlier revision carried it and no implementation
deferred on it, so a client could set it and silently get synchronous behaviour. A field that changes nothing is worse
than no field: it tells a caller a promise was made. Coop does not publish it and does not honour it.

## Compilation

1. The source is parsed as C# at language version `Latest`, as a single syntax tree.
2. The output kind is console application; the optimisation level is `Debug`; the assembly name is fixed.
3. The reference set is every `Microsoft.NETCore.App` assembly of the shared framework Coop runs on, plus every
   assembly the caller supplied and every assembly selected from a resolved package. Nothing else is referenced, so
   using any other assembly is a compile error.
4. The target framework is `net10.0`. The runtime configuration written beside the assembly names that framework,
   `Microsoft.NETCore.App` version `10.0.0`, and `rollForward: LatestMinor`.
5. Emission goes to a fresh staging directory that becomes the artefact: the assembly, its runtime configuration, and a
   copy of every caller-supplied and package-supplied reference assembly.

On success the artefact is handed to the execution step. On failure the outcome carries error diagnostics and nothing
else measurable: no assembly exists, no child process is started, and no timing is reported.

## Diagnostics are not output

Compilation and execution are separated in the outcome as well as in the pipeline. A compiler diagnostic never appears
in `standardOutput` or `standardError`, and run output never appears in `diagnostics`.

| Diagnostic field | Meaning |
| --- | --- |
| `id` | compiler diagnostic id, for example `CS0103` |
| `severity` | the compiler's severity, `Error` in every diagnostic Coop reports |
| `message` | the compiler's message |
| `filePath` | the submission path the compiler was given |
| `line` | 1-based line |
| `column` | 1-based column |

Only diagnostics of severity `Error` are reported. Warnings are discarded, so a submission that compiles with warnings
executes with no record of them.

## Contained execution

The child process is started as `dotnet exec <assembly>`, with the artefact directory as its working directory, no
window, and both standard streams redirected.

Containment differs by platform and the difference is observable:

| Platform | Containment | Effect |
| --- | --- | --- |
| Windows | The child is assigned to a job object with kill-on-close and a fixed 2 GiB per-process memory limit immediately after it starts, before it is waited on. | The child and its descendants are killed when the deadline expires, when the job is closed, or when Coop itself ends. A child that exceeds 2 GiB is terminated by the platform. |
| Other | No job object. | The child is killed as a process tree when the deadline expires. It is not memory-limited, and it is not killed by Coop ending. |

The memory ceiling is fixed and not configurable; it is a property of the containment, not a request or configuration
value.

## Deadline

| Property | Value |
| --- | --- |
| default | 60 seconds |
| ceiling | 600 seconds |
| above the ceiling | clamped to the ceiling, not refused |
| expiry | the job is terminated, then the process tree is killed, then Coop waits for exit |
| effect on the outcome | `deadlineExpired` is `true` and `exitStatus` is unmeasured, never zero |

The deadline timer starts at process start, so the budget covers process launch as well as the submission's own work.
The request carries `timeoutSeconds`, optional, default 60, clamped to a ceiling of 600. A request that asks for more
than the ceiling receives the ceiling rather than a refusal, because the caller asked for patience rather than made an
error.

## Outcome

One outcome is produced per execution.

| Field | Type | Meaning |
| --- | --- | --- |
| `executionId` | string | the identifier of the record this outcome was written to |
| `exitStatus` | integer or unavailable | the child's exit status; unmeasured when the deadline expired, when the submission failed to compile, and when the child never started |
| `standardOutput` | string or null | everything written to standard output, including what was written before a failure |
| `standardError` | string or null | everything written to standard error, including what was written before a failure |
| `deadlineExpired` | boolean | true when the child was killed at its deadline |
| `elapsedMilliseconds` | integer or unavailable | wall clock from child start to child exit |
| `processorMilliseconds` | integer or unavailable | processor time consumed by the child |
| `peakWorkingSetBytes` | integer or unavailable | peak working set of the child |
| `diagnostics` | array | compiler errors when the submission did not compile, otherwise empty |

Both streams are read concurrently for the whole life of the child, so a submission that writes a great deal to one
stream cannot block on the other.

Processor time and peak working set are read from a handle to the child obtained while it was running, which is why
they are available after exit. Where no job object can be created, both are unmeasured. They describe the child
process alone; a grandchild's processor time and memory are not included.

Within the model an unmeasured value is null. On every serialised surface and in text output it is the string
`unavailable`, never `0`, so a client must accept both a number and that string in the same field. This is a property of
the contract, not an error case.

Elapsed time covers the child process only. Compilation time is not measured and is not reported.

## Status

A record is written before the child starts and updated when the outcome exists. The status follows from the outcome
mechanically: `failed` when the deadline expired or the exit status is not zero, `completed` otherwise. A submission
that failed to compile is `failed`.

## Exit status of the invocation

A command-line invocation returns the submission's own exit status when one was measured. The codes Coop reserves for
its own failures are distinct from that pass-through:

| Code | Meaning |
| --- | --- |
| 1 | no exit status was measured, for example a compile failure |
| 2 | the command line was not usable |
| 3 | the run lock was not acquired within its budget |
| 4 | the data root could not be located |
| 5 | a package was refused |
| 6 | the execution identifier is unknown |

## Refusals

Execution has one refusal of its own and it is the package refusal, which is specified in the package specification and
happens before compilation. Everything else that can go wrong is either an outcome or a refusal owned by another
component.

A submission that does not compile is an outcome, not a refusal. The distinction is load bearing: a refusal means
nothing was compiled and nothing ran, so the caller must change its request; an outcome with diagnostics means the
compiler examined the submission and rejected it, so the caller must change its code. Reporting a compile failure as a
refusal would lead a caller to retry a submission that will fail identically.

## Limitations

1. A submission that supplies neither source text nor source path is not refused by name; the read fails and the
   invocation fails as an unhandled error. There is no contract-level check for the pair.
2. A null parameter map is not checked. The request is required to carry an object; a null fails the invocation rather
   than being read as empty.
3. Compiler warnings are dropped. A submission that compiles with warnings is executed with no trace of them.
4. The 2 GiB memory ceiling exists only where a job object does. On a platform without one the child is unbounded in
   memory and survives Coop ending.
5. Processor time and peak working set are unmeasured on any platform without a job object, and are unmeasured for a
   child that never started.
6. Compilation time is not measured, so `elapsedMilliseconds` cannot be used to compare the cost of a large submission
   with a small one end to end.
7. The command-line exit code cannot distinguish a submission that exited 1 from an invocation that measured no exit
   status. Only the outcome can.
8. Containment is process-level. The child shares the host's filesystem, network and environment; it is not a sandbox,
   and nothing restricts what it reads or writes.
9. `shouldRunAsync` does not defer compilation or execution on a surface that hands the caller the outcome; only a
   surface that acknowledges before running has anything to defer.

## Acceptance criteria

1. A submission that prints one line produces an outcome whose `standardOutput` contains the line preceded by nothing,
   with `exitStatus` 0 and `deadlineExpired` false.
2. A submission that writes to standard output and then throws produces an outcome carrying both the written output and
   a non-zero exit status, in one outcome.
3. A submission that does not compile produces an outcome whose `diagnostics` is non-empty, whose `exitStatus`,
   `elapsedMilliseconds`, `processorMilliseconds` and `peakWorkingSetBytes` are unmeasured, and whose
   `standardOutput` is empty.
4. A submission that loops forever with a 2 second deadline produces an outcome with `deadlineExpired` true, no exit
   status, and an `elapsedMilliseconds` no smaller than the deadline.
5. No child process of a deadline-expired submission remains after the outcome is returned.
6. A deadline requested above 600 seconds is clamped to 600 and accepted; it is not refused.
7. A submission that receives parameters `alpha=1`, `zeta=2` observes them in ordinal key order, and a value containing
   `=` arrives intact after its first `=`.
8. A submission that returns 7 produces an outcome with `exitStatus` 7 and an invocation exit code of 7.
9. A submission that uses an assembly outside the reference set fails to compile with a diagnostic naming the missing
   type or namespace.
10. Every serialised outcome represents an unmeasured number as the string `unavailable`, and never as `0`.
11. The record for an execution exists in the record store from before the child starts, and its terminal status is
    `failed` exactly when the deadline expired or the exit status is not zero.
12. Two submissions with identical source and different parameters each produce their own outcome and their own record,
    and the second does not report the first's output.
