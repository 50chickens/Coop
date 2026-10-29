# 09 - Acting on the skip plan

## Purpose

The plan component decides what must run. This document specifies the component that acts on that
decision: it builds once, tests only the projects the plan allows, records what it observed, and
reports every project's treatment so a skip can never be read as a pass.

The component owns the run, the report and the exit code. It does not own the hashing rule
(08-plan.md), a solution's criteria beyond "the project's tests exit zero", or any project's
configuration.

## Inputs

| Input | Required | Rule |
| --- | --- | --- |
| Solution file | Yes | Its projects define the report. |
| Configuration | Yes, default from configuration | Hashed by the plan and passed to the build and to each test invocation. |
| Platform | Yes, default from configuration | Hashed by the plan and passed to each per-project test invocation. It is not passed to the solution build. |
| Plan document | Yes | Produced by the plan component in the same invocation, against the same solution, configuration and platform. A report built from a plan for another configuration is invalid. |
| Recorded results | No | Read by the plan; written by this component after an observed outcome. |
| Run-everything switch | No | See Running everything regardless. |
| Project selector | No | See Narrowing to one project. |

## Ordering

The report order is a topological order of the project-reference graph, depth-first from the
solution's project list: a referenced project precedes every project that references it. A project
appears once. Projects that reference nothing keep the solution's relative order.

Two orders are derived from the same graph:

- The run order, which is the topological order restricted to projects whose effective
  decision is `run`.
- The report order, which is the topological order over every project in the solution.

A dependency outside the solution is not a report entry and does not affect blocking.

## Execution sequence

1. Compute the plan.
2. Apply the run-everything switch and the project selector to produce each project's effective
   decision.
3. Initialise every project's outcome to `not run`.
4. If at least one project must run, build the solution once, in the chosen configuration. The
   build is the only compilation step; per-project test invocations do not build.
5. Walk the report order. For each project:
   - If its decision is `skip`, report it skipped.
   - Else if the solution build failed, report it `not run` with a reason naming the build's exit
     code.
   - Else if any project it references, directly or transitively, recorded `fail` or `not run`,
     report it `not run` with the name of the project that stopped it.
   - Else invoke that project's tests and report `pass` or `fail` from the exit code.
6. Write one record for every project that ran, carrying the hash it ran under and its observed
   outcome.
7. Emit the report and the verdict.

Rules that the sequence makes explicit:

- A plan whose every entry is `skip` builds nothing. Not even the solution build runs.
- A test invocation per project, sequentially, one at a time. Two test invocations against one
  solution's shared intermediate output collide, which is why the build is separate and the test
  invocation does not build.
- The result of a project is its process exit code, not a parse of its output. Console text is a
  rendering that varies with logger, verbosity, framework version and terminal width; a parse that
  stops matching does not fail, it finds no failures and reports a pass. Captured output is kept
  for diagnosis and printed only when a project fails.
- A project that ran writes exactly one record. A skipped project writes none, so a skip never
  rewrites the record it relied on.
- A project that did not run writes nothing. `not run` therefore cannot be mistaken for a pass.
- A solution build failure records nothing. The record's meaning is "this hash passed its tests";
  a failed build is evidence about no project's tests. The verdict is still `failed` for that run.

## Outcome vocabulary

| Value | Field | Meaning | Counted in |
| --- | --- | --- | --- |
| `run` | `decision` | The project must be built and tested in this invocation. | `run` if it then passed or failed, `notRun` if it did not run |
| `skip` | `decision` | The plan allowed the project to be skipped and the invocation honoured that. | `skipped` |
| `pass` | `outcome` | The project's tests were invoked in this run and exited zero. | `run` |
| `fail` | `outcome` | The project's tests were invoked in this run and exited non-zero. | `run` |
| `skipped` | `outcome` | The project was not run, on a recorded pass. | `skipped` |
| `not run` | `outcome` | The project was required to run and did not, because of a build failure or a blocked dependency. | `notRun` |

`decision` and `outcome` are separate fields and must both be reported. A project can have
`decision` `run` and `outcome` `not run`; it can never have `decision` `skip` and `outcome` `pass`,
because a skip did not test anything in this run.

## Reporting what was skipped, and why

A skip is reported, per project, with:

- The project identifier.
- The hash the decision was made on.
- The decision `skip`.
- The recorded outcome relied on, its timestamp, the machine and the user that recorded it.

Text form, one line per skipped project:

```
<name>  <hash>  skip  relied on pass recorded 2026-10-23T10:40:39.7720305+00:00 on AL-LAPTOP by morrisal
```

Text form, one line per project that ran:

```
<name>  <hash>  run  pass  exit 0 in 1931 ms  no recorded result
```

Text form, one line per project that did not run:

```
<name>  <hash>  run  not run  not run because <dependency name> failed
```

The trailing reason is the plan's reason for a run, or, when the run-everything switch overrode a
skip, the fixed text `--all requested; the recorded pass was ignored`. A project that ran reports
the plan's reason unchanged, so a reader can see whether it ran because nothing was recorded or
because its input moved.

Four reason texts are fixed and are not composed from the plan:

| Situation | `reason` |
| --- | --- |
| The run-everything switch overrode a skip | `--all requested; the recorded pass was ignored` |
| A referenced project failed | `not run because <name> failed` |
| A referenced project did not run | `not run because <name> was not run` |
| The solution build failed | `the solution build failed with exit code <code>` |

The summary is three counts and a verdict:

```
run: 0  skipped: 8  not run due to an earlier failure: 0
verdict: passed  in 6305 ms
```

`run` counts projects that executed in this invocation, `pass` and `fail` together. `skipped`
counts projects the plan allowed to be skipped and that were skipped. `not run` counts projects
that were required to run and did not.

## Running everything regardless

One switch, `--all`, replaces every `skip` decision with `run` for this invocation only.

- Every project is built and tested, whether or not a recorded pass exists.
- Each affected project's reason states that the recorded pass was ignored, in the fixed text under
  Reporting what was skipped, and why, so an audited `--all` run is distinguishable from an
  incremental one.
- Records are still written for every project that ran, so an `--all` run refreshes the record set.
- A recorded pass is not deleted by `--all`. Deleting it would mean a subsequent incremental run
  had no evidence at all for that project.
- `--all` does not bypass blocking: a failed project still stops its dependants.

`--all` is the release-cut behaviour: the same command, one switch, no second gate.

## Narrowing to one project

One selector, `--project <name>`, restricts the report to the named project and every project that
references it, directly or transitively, in the same topological order. Projects outside that set
are absent from the report rather than reported `not run`, because they were not asked about. A
name absent from the plan is a refusal.

The selector composes with `--all`: the narrow set is built and tested regardless of records.

## The report document

```json
{
  "schemeVersion": "1",
  "solution": "<absolute solution path>",
  "configuration": "Release",
  "platform": "AnyCPU",
  "machine": "<machine name>",
  "user": "<user name>",
  "durationMilliseconds": 108046,
  "buildExitCode": 0,
  "buildMilliseconds": 2968,
  "projects": [
    {
      "name": "<project identifier>",
      "path": "<path relative to the solution directory>",
      "hash": "<64 hex characters>",
      "decision": "run",
      "outcome": "pass",
      "reason": "no recorded result",
      "exitCode": 0,
      "durationMilliseconds": 1931,
      "output": "<combined standard output and standard error>",
      "reliedOn": null
    }
  ],
  "run": 8,
  "skipped": 0,
  "notRun": 0,
  "verdict": "passed"
}
```

Rules:

- `buildExitCode` is null and `buildMilliseconds` is 0 when nothing was built.
- `exitCode` is null for `skipped` and `not run`; it is present for `pass` and `fail`.
- `durationMilliseconds` per project is the wall time of that project's test invocation.
- `output` is present for every project that ran and is retained for `pass` as well as `fail`, so a
  consumer can assert on captured output without a second run. Only the text report suppresses it
  for a pass.
- `reliedOn` is non-null if and only if `outcome` is `skipped`, and is the plan's `reliedOn`
  object unchanged.
- `run + skipped + notRun` equals the length of `projects`.

## Verdict and exit codes

The verdict is `passed` if and only if every project in the report is `pass` or `skipped`.
A single `fail` or `not run` makes it `failed`.

| Exit code | Condition |
| --- | --- |
| 0 | Verdict `passed`. |
| 1 | Verdict `failed`, or the report could not be produced. |
| 2 | The command line was not usable: the project selector names a project absent from the solution. |
| 4 | The data root cannot be located, or the solution file does not exist. |

A plan that cannot be computed exits with the plan component's own code (08-plan.md, Refusals) and
prints no report. The codes above are the product's shared refusal codes and carry the same meaning
here as on every other surface.

`passed` therefore means "every project either ran and passed in this invocation, or was skipped
on a recorded pass". It never means "no project failed".

## Refusals

| Condition | Behaviour |
| --- | --- |
| Solution root unresolvable | Message naming the argument to pass; exit 4; no report. |
| Solution file absent | Message naming the resolved path; exit 4; no report. |
| Project selector names an unknown project | Message naming the project and the solution; exit 2; no report. |
| The plan cannot be computed | The plan component's message; no report; no record written. |
| The solution build exits non-zero | Not a refusal of the command: every project that had to run is reported `not run`, the build output is printed, no test is invoked, no record is written, verdict `failed`, exit 1. |

## Limitations

- A project whose test invocation runs zero tests exits zero and is recorded as a pass. A project
  that lost its test adapter, and a library project with no tests at all, are indistinguishable
  from a project whose tests passed. The record says a pass happened; it does not say a test ran.
  Closing this needs a count of executed tests, which is either an output parse or a results file.
- Every gap in the plan's input set reaches the verdict. If a change lands in an input the plan does
  not hash, no hash moves, every project skips, nothing is built, and the verdict is `passed` on a
  tree whose compilation may be broken. The gate is where that gap is felt.
- `--all` with a failure in a dependency leaves the dependants' earlier pass records in place,
  because they did not run and this component records only what it observed. Those passes were
  genuine at that hash, so a later incremental run may skip a project an `--all` run intended to
  re-run but could not. It is not a false pass; it is the one case where "did not run" and "has a
  pass on record" are both true.
- The component holds no lock. Two invocations against one solution at once collide in the shared
  build and intermediate output. The single-run lock that protects submissions is not used here.
- The platform is deliberately not passed to the solution build. A solution-level platform that
  does not match the solution's own configuration mapping is rejected by the build engine, so the
  configuration goes to the build and the platform goes to each per-project test invocation. A
  caller must pass a platform the projects honour.
- A skip is not free. Most of the cost of an all-skipped run is the plan's per-project evaluation;
  measured, an eight-project solution took 6305 ms with every project skipped against 108046 ms
  with every project run, and a later ten-project measurement was 6175 ms skipped and 110267 ms
  run. The cache removes build and test time, never the hashing that proves the tree is unchanged.
- The component tests every project, including projects with no tests. It carries no list of which
  projects are test projects, because a library's empty test invocation is cheap and a maintained
  list is a second source of truth that goes stale.

## Acceptance criteria

1. A plan in which every project is `skip` invokes no build and no test, reports every project
   `skipped`, and yields verdict `passed`.
2. Every skipped project's report carries its hash and the relied-on record's outcome, timestamp,
   machine and user, and the relied-on document's `recordedAt` is unchanged after the run.
3. A plan with exactly one unrecorded project invokes exactly one solution build and exactly one
   test invocation, and reports that project `pass` or `fail`.
4. `--all` runs every project even when every project has a recorded pass, reports `run` as the
   decision for each, and states in each reason that the recorded pass was ignored.
5. A failing library stops its direct and transitive dependants; each is reported `not run` with the
   name of the project that stopped it; only the library's tests were invoked; the verdict is
   `failed`; the counts are consistent with `run + skipped + notRun`.
6. A failed project's `fail` record survives the run, and the next plan reports `run` for that
   project with the reason `last recorded outcome was fail`.
7. A solution build failure reports every project that had to run as `not run`, invokes no test,
   writes no record, and exits 1.
8. `--project <name>` restricts the report to that project and its dependants, in dependency order,
   and leaves every other project out of the report.
9. `--project <unknown>` exits 2 and prints no report.
10. An unresolvable root exits 4 and prints no report.
11. The report document round-trips through JSON and satisfies
    `run + skipped + notRun == projects.length`, with `reliedOn` non-null exactly on skipped
    entries.
12. The text report prints one line per project containing the identifier, the hash and the
    decision, and the summary line contains all three counts and the verdict.
