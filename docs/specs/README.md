# Coop specifications

Fourteen specifications, one per component. Each describes behaviour, contract, refusal conditions and acceptance
criteria, and none names a file, folder or class, so any implementation structure is compatible with them.

They were extracted from a working reference implementation's documentation, source and measured reports. Extraction
rather than transcription is why this folder also carries a limitations register: where the reference implementation's
behaviour contradicted its own documentation, or was published but not implemented, the specification states the
intended behaviour and records the gap rather than inheriting it silently.

## Read in this order

| # | Specification | Defines |
| --- | --- | --- |
| 1 | [Execution](01-execution.md) | The submission, the request, compilation, contained execution, deadline enforcement, the outcome |
| 2 | [Locking](02-locking.md) | Serialising concurrent invocations, the wait budget, the refusal |
| 3 | [Hashing](03-hashing.md) | Artefact and result content keys, and what each must include |
| 4 | [Packages](04-packages.md) | Exact-version package resolution, isolation, asset selection |
| 5 | [Records](05-records.md) | One record per execution, the derived index, staleness |
| 6 | [Template](06-template.md) | The self-describing contract the product serves |
| 7 | [Artefacts](07-artefacts.md) | Content-addressed storage and the reuse marker |
| 8 | [Plan](08-plan.md) | Per-project input hashing and the skip plan |
| 9 | [Gate](09-gate.md) | Acting on the plan, and reporting what was skipped |
| 10 | [Surface](10-surface.md) | The MCP tool and the HTTP endpoint |
| 11 | [Watch](11-watch.md) | Claim-by-rename consumption of a watched folder |
| 12 | [Pipeline](12-pipeline.md) | Build, test, package, release and the container image |
| 13 | [Agent](13-agent.md) | Microsoft Agent Framework integration and the Agent Skill |
| 14 | [Issue workflow](14-issue-workflow.md) | An issue as a prompt: trigger gate, engine handover, validation, and the route back to the host |
| 14 | [Issue workflow](14-issue-workflow.md) | An issue as a prompt: the trigger gate, the engine contract, validation, git ownership, completion |

Execution comes first because every other specification keys on the contract it defines. A change to it is breaking.
The issue workflow is last because it consumes the gate of the pipeline specification and contributes no contract that
another component keys on.

## Shared exit codes

These are the product's own process exit codes. They are distinct from the exit status of a compiled submission, which
is carried inside the outcome rather than in the product's own code.

| Code | Meaning |
| --- | --- |
| 0 | Success |
| 1 | No exit status was measured, for example a compile failure |
| 2 | The command line was not usable |
| 3 | The run lock was not acquired within its budget |
| 4 | The data root could not be located, or the solution file does not exist |
| 5 | A package was refused |
| 6 | The execution identifier is unknown |
| 7 | The surface refused to bind a non-loopback address without an explicit grant |
| 8 | The trigger was refused: not an issue, author not permitted, or automation origin |
| 9 | The engine could not start: executable not found, model credential unobtainable or unsealed, or launch failure |
| 10 | The work was refused before the engine ran: working copy not clean, or the branch already exists |
| 11 | A version control operation failed: commit, push, pull request, comment or close |

Codes 1 to 6 are shared across components. Code 7 belongs to the surface alone. Codes 8 to 11 belong to the issue
workflow.

A refusal stops a run before it starts work. A condition arising during a run is an outcome with a recorded verdict,
not a refusal. That single rule is why a failed gate is an outcome and not a refusal code.
| 8 | The trigger was refused: not an issue, author not permitted, or the event originates from an automation |
| 9 | The engine could not start: executable absent, model credential unavailable, or launch failed |
| 10 | The work was refused before the engine ran: dirty working copy, or the branch already exists |
| 11 | A version control operation failed: commit, push, pull request, comment or close |

Codes 1 to 6 are shared across components. Code 7 belongs to the surface alone. Codes 8 to 11 belong to the issue
workflow alone. The reference implementation used 7 for nothing and reused 6 for the bind refusal, which would have made
"unknown execution" and "refused to listen" indistinguishable to a caller.

## Known limitations carried forward

These are defects and gaps found in the reference implementation while extracting the specifications. Each is recorded
here, and in the specification it touches, so that Coop does not silently reproduce it. They are candidates for the
first units of work after each component exists.

### Keys and caching

1. Build version is composed outside the result key, so a printed key and a stored document name can disagree for the
   same execution.
2. The runtime-identifier input to the artefact key is never populated, so one of its three declared inputs is dead and
   a runtime-specific artefact can collide with another.
3. Compile failures are never cached, so a submission that does not compile is recompiled on every attempt. The case
   where caching matters most for an agent iterating on a compile error is the case that is not cached.
4. Transitive package versions resolve cache-first and the resolved closure is not recorded, so the recorded package set
   is not sufficient to reproduce a result.

### Contract against behaviour

5. The template's response section omits the reuse block that the surfaces actually return.
6. The template states that a request has no timeout field while the network surface accepts one.
7. The template declares unmeasured numeric values as the string `unavailable` while outcomes emit `null`.
8. `shouldRunAsync` is published in the request contract but never defers execution.
9. A request supplying neither source field fails without a named refusal.

### Surfaces and records

10. A similarity query exists in the record store with no surface operation that reaches it.
11. The record has no query operation on any surface; a caller can fetch by identifier but cannot search.
12. No MCP execution tool exists in the reference implementation. The MCP tool contract in
    [Surface](10-surface.md) is specified from the product's own semantics, not measured.

### Gate and skip correctness

13. Only the compile item type is hashed, so a change to an embedded resource can produce a false skip.
14. A project with zero tests records a pass, which cannot be distinguished from tests that ran and passed.
15. Running everything regardless can leave stale pass records behind for projects whose tests were deleted.
16. The gate holds no lock, so it can run concurrently with itself.

### Environment and reporting

17. Container runs report null processor time and peak working set, because both are read from a handle that exists only
    on Windows. Cost statistics are therefore unavailable on the container path.
18. Publishing is framework-dependent, so the runtime identifier recorded in a release archive is a label rather than a
    property of what was published.

### Refusal consistency

19. A lock timeout is exit 3 on the command surface but a recorded failed execution elsewhere. The specifications adopt
    a single rule: a condition that prevents an execution from starting is a refusal with a code, and a condition
    arising during an execution is a recorded outcome.

### Issue workflow

20. The engine inherits the orchestrator's version control token in its process environment, so the rule that the engine
    never holds that credential is stated but not enforced, and the engine's shell grant can use it. Every other rule
    in the specification assumes this one is closed.
21. The prompt is committed into the repository as a file, so untrusted issue text is recorded by design.
22. No declared-path check exists, so a change that edits the gate or the workflow definition passes on the strength of
    the gate it just rewrote.
23. Validation precedes the commit but the commit is taken with an add-all, so the committed content is not proven to
    be the content that was validated. It holds today only because build output happens to be ignored.
24. The issue closes on a green gate rather than on a merge, so it can be closed while its pull request is open.
25. No branch-attempt discriminator exists, so a legitimate retry of the same issue fails at branch creation.
26. Nothing bounds the engine except the job timeout, so a wedged engine holds the only runner instance for the full
    timeout and queues everything behind it.
27. Required status checks need branch protection, which a private repository without a paid plan cannot have, so the
    verdict is advisory and a red run can still be merged.
28. A pull-request-triggered validation workflow executes the pull request's own code. Where it targets the same
    self-hosted runner as the orchestrator, the separation of orchestrator and validation environment is defeated.
29. Nothing prevents a permitted human from editing the workflow definition on the default branch directly.
30. The engine's event vocabulary is parsed for the reply text, so a change to that vocabulary yields an empty comment
    with no error.
31. Tool counts are captured and logged but nothing acts on them, so an engine that failed every tool call and then
    answered in prose is indistinguishable from one that worked.
