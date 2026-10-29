# 08 - Per-project input hashing and the skip plan

## Purpose

A build-test run costs what its slowest project costs, and that cost is paid again for a tree
that has not changed. This document specifies the component that decides, before anything is
built or tested, which projects of a solution must run and which can be skipped.

The component does three things and nothing else:

1. It computes one content hash per project, over every input that project compiles.
2. It compares each hash with the last recorded outcome for that project.
3. It emits a plan stating, per project, a decision of `run` or `skip`, the hash the decision was
   made on, and - for a skip - the recorded outcome the skip relied on.

It does not build, does not test, and does not own a solution's success criteria.

## Definitions

| Term | Meaning |
| --- | --- |
| Project | One buildable unit named in a solution file. Its identifier is its file name without extension. |
| Input set | Every file and property listed under What the input hash covers that the project's compilation depends on. |
| Project hash | Lowercase hexadecimal SHA-256 over the canonical encoding of the input set (see How the hash is computed). Always 64 characters. |
| Recorded result | The stored document describing the last outcome observed for one project, under one hash, one solution, one configuration and platform, on one machine. |
| Decision | `run` or `skip`, per project. The only two values. |
| Plan document | The JSON document defined under The plan document, reporting the context, the per-project hash and the decision. |
| Scheme version | A constant string that identifies the hashing rule. It is part of the hash, so a change to how a hash is computed changes every hash. |

## What the input hash covers

Every row is a field of the canonical descriptor. Changing any one of them changes the hash.

| Input | Read from | What breaks if it is omitted |
| --- | --- | --- |
| The project file itself | SHA-256 of the project file bytes | A changed package reference, target framework, property or project reference leaves the hash identical, so every project plans `skip` after an edit to its own project file. |
| Every source file the project compiles | The build engine's own evaluated item list for the compile item type, requested with an evaluation-only invocation (no build), then SHA-256 per file | A fixed bug, an added file or a deleted file does not move the hash, so the test that would catch the fix is skipped. |
| Every build props and build targets file from the project directory up to the filesystem root, plus the nearest global tooling version file | Walking up from the project directory | A property that changes compilation for every project (language version, implicit imports, nullable context, an exclude rule) changes no project file and no source file, and would plan `skip` everywhere. |
| The build configuration and platform | The values the build engine reports for the evaluated project, not the values echoed back from the caller | A result recorded for one configuration is reused by a plan for another. |
| Every resolved package, by id and version | The restore assets document for the project, restricted to entries whose type is a package, read from the target-framework section being built | Declared versions cannot express a transitive resolution; a restore that moves a transitively resolved package would plan `skip` while the assembly produced changes. |
| Every referenced project, transitively, by that project's own hash | The evaluated project-reference items, hashed depth-first | Editing a library leaves its dependants' hashes unchanged, so the tests whose subject changed are skipped. |
| The target framework | The value the build engine reports for the evaluated project | The assets section read for the hash would not correspond to the framework being built, and a framework change arriving from an imported file would be invisible. |
| The hash scheme version | A constant in the component | A change in how the plan is computed would be compared against records produced by the old computation. |

The compile file list is the build engine's answer, not a reimplementation of the SDK's file
globs. An item list produced by evaluation is the same list the build will use; a hand-written
pattern is a second opinion that eventually disagrees.

## How the hash is computed

The descriptor is a sequence of named fields, appended in a fixed order, then hashed once.

Encoding rules, applied to every field:

- Append the field name, then a newline.
- Append the character count of the value in decimal invariant form, then a newline.
- Append the value, then a newline.
- A null value is appended as `-1` followed by a newline, and nothing else, so null and the empty
  string are distinguishable.

Field order:

| Order | Field name | Value |
| --- | --- | --- |
| 1 | `project-key-scheme` | The scheme version constant. |
| 2 | `project` | The project file path relative to the solution directory. |
| 3 | `project-file` | Hash of the project file bytes. |
| 4 | `configuration` | Evaluated configuration. |
| 5 | `platform` | Evaluated platform. |
| 6 | `target-framework` | Evaluated target framework. |
| 7 | `build-file-count`, then `build-file-path` and `build-file-hash` per file | Sorted ordinal by path. |
| 8 | `compile-file-count`, then `compile-file-path` and `compile-file-hash` per file | Sorted ordinal by path. |
| 9 | `package-resolved` | `yes` or `no`. |
| 10 | `package-count`, then `package-id` and `package-version` per package | Sorted by id, case-insensitively, then by version, ordinal. |
| 11 | `reference-count`, then `reference-path` and `reference-hash` per reference | Sorted ordinal by path. |

The digest is SHA-256 over the UTF-8 bytes of the descriptor, rendered as lowercase hexadecimal.

A referenced project's hash is computed before the dependant's and appears in the dependant's
descriptor as `reference-path` plus `reference-hash`. A cycle is impossible in a buildable
solution, and a reference encountered twice is hashed once.

Paths in the descriptor are relative to the solution directory and use the platform's directory
separator, so the same tree hashed from two working directories produces the same hash.
Content of referenced assemblies is not hashed; the referenced project's own input set is.

## The recorded result

One JSON document per project per solution. Storage rules:

- Documents live outside the build output, in a directory keyed by the solution file name, with
  one document per project named for the project identifier.
- Reading a document that is absent returns nothing; it is not an error.
- A write is atomic: the new content is written to a temporary name and moved over the target, so
  a reader never sees a partial document.
- A record is authoritative once written. Nothing rewrites or deletes a record except the step
  that observed a new outcome for that project.

```json
{
  "schemeVersion": "1",
  "solution": "<absolute solution path>",
  "project": "<project identifier>",
  "projectPath": "<path relative to the solution directory>",
  "configuration": "Release",
  "platform": "AnyCPU",
  "machine": "<machine name>",
  "user": "<user name>",
  "hash": "<64 hex characters>",
  "outcome": "pass",
  "recordedAt": "2026-10-23T10:40:39.7720305+00:00"
}
```

`outcome` is the vocabulary owned by the acting component (09-gate.md); `pass` is the only value
that permits a skip.

## The plan document

```json
{
  "planVersion": 1,
  "schemeVersion": "1",
  "solution": "<absolute solution path>",
  "configuration": "Release",
  "platform": "AnyCPU",
  "machine": "<machine name>",
  "user": "<user name>",
  "durationMilliseconds": 6305,
  "projects": [
    {
      "name": "<project identifier>",
      "path": "<path relative to the solution directory>",
      "hash": "<64 hex characters>",
      "references": ["<project identifier>"],
      "recordedResultExists": true,
      "decision": "skip",
      "reason": "hash unchanged; recorded pass at 2026-10-23T10:40:39.7720305+00:00 on AL-LAPTOP by morrisal",
      "reliedOn": {
        "schemeVersion": "1",
        "solution": "<absolute solution path>",
        "project": "<project identifier>",
        "projectPath": "<path relative to the solution directory>",
        "configuration": "Release",
        "platform": "AnyCPU",
        "machine": "AL-LAPTOP",
        "user": "morrisal",
        "hash": "<64 hex characters>",
        "outcome": "pass",
        "recordedAt": "2026-10-23T10:40:39.7720305+00:00"
      }
    }
  ]
}
```

Rules for the document:

- Property names are camelCase; the document is indented when serialised for a human reader.
- `planVersion` and `schemeVersion` are integers-as-documented and string respectively; a consumer
  that does not know `planVersion` must refuse the document rather than guess.
- `projects` is ordered as the solution lists its projects, which is the build order the plan
  claims nothing about.
- `references` names only projects present in the solution and present in `projects`.
- `recordedResultExists` is true when any document was read for that project, whatever its hash.
  It is not a substitute for `decision`.
- `reliedOn` is null for a `run` decision and is present for every `skip`. A skip without
  `reliedOn` must be treated as invalid by a consumer.
- `durationMilliseconds` is the wall time the plan took, including the evaluation of every
  project. It is measured, not estimated.

The text form prints one header block (`solution`, `configuration`, `platform`, `machine`, `user`),
one summary line (`projects` count and the duration) and one line per project:

```
<name>  <hash>  <decision>  <reason>
```

## The decision

For each project, in the order the solution lists them, the first matching rule wins. The reason
text is the value of `reason` and is part of the contract, because it is what makes a skip
auditable.

| Order | Condition | Decision | `reason` |
| --- | --- | --- | --- |
| 1 | The project's resolved package set is unknown because the restore assets document is absent | `run` | `resolved packages unknown because the restore assets document is missing` |
| 2 | No recorded result exists | `run` | `no recorded result` |
| 3 | The record names a different solution | `run` | `recorded under solution <solution>` |
| 4 | The record names a different configuration or platform | `run` | `recorded under configuration <configuration> and platform <platform>` |
| 5 | The record names a different machine | `run` | `recorded on machine <machine>` |
| 6 | The record names a different user | `run` | `recorded by user <user>` |
| 7 | The record's hash differs from the computed hash | `run` | `inputs changed since the recorded result` |
| 8 | The record's outcome is not `pass` | `run` | `last recorded outcome was <outcome>` |
| 9 | Otherwise | `skip` | `hash unchanged; recorded pass at <recordedAt, round-trip ISO-8601> on <machine> by <user>` |

Rule 8 is what makes a failure sticky in the right direction: a failed project re-runs until it
passes, and a failure is never inherited as a skip.

Rules 2 to 8 make the plan conservative in one direction only. A plan may report `run` for a
project that would have passed; it must never report `skip` for a project whose inputs moved.
Every rule that cannot be evaluated - an unreadable record, an unknown configuration - resolves to
`run`.

## Detecting a stale result

A recorded result is stale when any of the following holds. Each corresponds to a rule above and
is the only evidence needed to reject reuse.

| Stale because | Field compared |
| --- | --- |
| The tree changed | `hash` |
| The build was configured differently | `configuration`, `platform` |
| The solution is not the one recorded | `solution` |
| The machine differs | `machine` |
| The user differs | `user` |
| The hashing rule changed | `schemeVersion` is a field of the hash, so `hash` moves |
| The last attempt failed | `outcome` |

Nothing else invalidates a record. A record is never aged out by time, and a record from an older
tool version is reused if the scheme version and hash still match.

## Refusals

| Condition | Behaviour | Exit code |
| --- | --- | --- |
| The solution root cannot be resolved from the working directory or an explicit argument | Message naming the argument to pass; nothing is computed | 4 |
| The named solution file does not exist | Message naming the resolved path | 4 |
| A record request is not of the form `<project>=<outcome>` | Message naming the malformed argument | 2 |
| A record request names a project absent from the solution | Message naming the project and the solution | 2 |
| A record request names a project whose resolved package set is unknown | Message naming the project and the reason | 2 |
| An evaluated project declares several target frameworks | Message naming the project and the frameworks; the plan is not produced | non-zero (failure) |
| The build engine evaluation of a project exits non-zero | Message carrying the exit code and the engine's standard error; the plan is not produced | non-zero (failure) |

A refusal produces no plan. A partially computed plan is worse than none, because a caller cannot
tell which projects were considered.

## Limitations

- Only the compile item type is hashed. A file reaching the build as an embedded resource, or as
  content copied to the output, is not an input to the hash. An edit to such a file moves no hash
  and plans `skip` for every project that consumes it.
- Compile items produced by a build target during the build (generated assembly attributes, source
  generators) are absent from an evaluation-only item list. The properties they derive from are
  hashed; a default that changes inside a new SDK is not visible.
- Props and targets imported from packages are not read. The resolved package identity is hashed,
  so a version change is caught and a locally altered package payload is not.
- The SDK, runtime and compiler versions are not inputs. A new SDK that changes a compiler default
  or a default item type changes a build without moving any hash.
- A build props file above the solution directory is hashed because the walk reaches the
  filesystem root. If the walk stopped at the solution directory, such a file would be a false skip
  for every project.
- Solution-level platform mapping is not read. The platform hashed is the project-level platform,
  so a caller must name a platform the projects honour.
- A project that has never been restored has no resolved package set and therefore always plans
  `run`. It is never skipped, which is correct but not cheap.
- Record documents are keyed by project identifier. Two projects with the same file name in one
  solution would collide; a solution that contains such a pair is refused by neither the plan nor
  the record, and one project's record would be read for the other.
- The plan is a decision, not a guarantee. Nothing checks that a project skipped as a pass would
  actually pass now; the hash is the only evidence.
- Producing a plan costs one build-engine evaluation per project in the project-reference closure,
  roughly 0.6 to 0.8 seconds per project on a warm restore. A ten-project solution measured 6.0 to
  6.5 seconds whether or not anything ran. That floor is the price of the guarantee, and it is not
  removable by caching the plan itself without reintroducing the stale plan this component exists
  to prevent.

## Acceptance criteria

1. A solution with no recorded results produces a plan with one entry per project, every
   `decision` equal to `run`, and every `reliedOn` null.
2. A recorded `pass` at the current hash produces `decision` equal to `skip`, and `reliedOn` carries
   the same `hash`, `outcome`, `recordedAt`, `machine` and `user` as the stored document.
3. Editing one source file in a project produces `run` for that project with the reason
   `inputs changed since the recorded result`, and leaves every project that does not reference it
   at `skip`.
4. Adding, deleting or renaming a source file in a project changes that project's hash.
5. Editing a build props file that applies to every project changes the hash of every project that
   imports it.
6. Changing the resolved version of a package that no project file declares changes the hash of
   every project that resolves it.
7. Editing a referenced project changes the hash of every project that references it, directly or
   transitively, and changes no unrelated project's hash.
8. A record written under a different configuration, a different solution, a different machine or
   a different user produces `run`, with the reason naming the differing field.
9. A recorded `fail` produces `run` with the reason `last recorded outcome was fail`.
10. Every hash is 64 lowercase hexadecimal characters and is reproducible across runs of the same
    tree from different working directories.
11. A project whose restore output is absent produces `run`, and the plan is still produced.
12. The plan document round-trips: it serialises to JSON, deserialises to the same values, and a
    consumer can decide from `decision` and `reliedOn` alone.
13. An unresolvable root or a missing solution exits 4 and prints no plan.
14. A malformed or unknown record request exits 2 and writes no document.
