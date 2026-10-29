# Artefacts and results

Two content-keyed stores stand behind execution. The artefact store holds compiled output under the artefact key. The
result store holds outcomes under the result key. A reused answer is marked, so a client can always tell an outcome that
was produced by its own run from one that was produced earlier and returned again.

## The artefact store

One directory per artefact key holds everything the child process needs:

| Content | Purpose |
| --- | --- |
| the compiled assembly | what is executed |
| the runtime configuration | names the target framework, the framework and the version roll-forward policy |
| a copy of every caller-supplied and package-supplied assembly | so the child resolves them from its own directory without a dependency manifest |

The directory is not copied into the store. Compilation writes into a fresh staging directory whose name is a random
identifier, and that directory is renamed into place under the key once the artefact is complete. Three properties come
from that:

1. A store write is a rename, so it costs nothing proportional to the size of the artefact.
2. An artefact is either entirely present or entirely absent, because a partially written directory is never the
   destination of a rename.
3. A key is claimed once. The rename refuses an existing destination, so a second write for a key that already exists
   fails rather than merging two artefacts.

A read is a hit when the assembly file exists at the key's path, and returns the assembly path and the directory.

## The result store

One JSON document per key records the outcome that key produced.

| Field | Meaning |
| --- | --- |
| `resultKey` | the key this document is stored under |
| `artefactKey` | the artefact the outcome came from |
| `runnerVersion` | the informational version of the build that captured it |
| `executionId` | the execution that produced it |
| `recordedAt` | when it was recorded, UTC |
| `outcome` | the outcome, as returned to the caller that produced it |

The document name is not the bare result key. It is a further hash over the result key and the build's informational
version, so a capture change invalidates every stored result without a constant anyone must remember to bump. A read
also compares the document's recorded version with the running one and treats a mismatch as a miss, so a document
reached under an unexpected name is still refused.

## What happens on an execution

The two levels are consulted in order, and each level has its own observable result.

| Situation | Artefact level | Result level | What runs |
| --- | --- | --- | --- |
| first execution of this source and these parameters | miss | miss | compile and execute |
| same source, different parameter values | hit | miss | execute only |
| same source, same parameters, same build | hit, if consulted | hit | nothing: the stored outcome is returned |
| source edited | miss | miss | compile and execute |
| build version changed | unchanged | miss | compile and execute |

A hit at the result level ends the execution: the compile is not attempted and no child process starts. A hit at the
artefact level skips the compile and still starts a child, because only the result level can prove that these
parameters have been answered.

## The reuse marker

A run reports both levels, and when the result level hit it also reports who produced the answer.

```
"cache": {
  "artefactKey": "<64 hex characters>",
  "artefactReused": false,
  "resultKey": "<64 hex characters>",
  "resultReused": true,
  "reusedResult": {
    "executionId": "<execution that produced this outcome>",
    "recordedAt": "<ISO-8601 with offset>",
    "runnerVersion": "<build that captured it>",
    "hint": "the timings in this response are those of execution <id> recorded at <time>, not of this run"
  }
}
```

The same marker is written into the record as `reused`, so it survives the response and is readable later by execution
identifier. In text output the marker is printed above the numbers, because the numbers that follow it are not this
run's.

## Why reuse must be distinguishable from fresh

The value of a hit is that it is cheap. The danger is that it looks exactly like a run.

1. The timings belong to another execution on another machine at another moment. `elapsedMilliseconds`,
   `processorMilliseconds` and `peakWorkingSetBytes` are returned unchanged from the recorded outcome. A caller that
   averages them with fresh measurements is measuring its cache hit rate, not its submission.
2. The output belongs to another run of the same program. Any dependence on the clock, the machine, the network or the
   filesystem is frozen at the moment the stored outcome was written, and a caller that reads the output as evidence
   about now is reading evidence about then.
3. The stored answer was captured by a specific build. The marker names the build's version, so a reader can tell
   whether the capture that produced it is the one it trusts.
4. An execution identifier must resolve. A hit is given its own execution identifier and its own record, so a caller
   that was handed an answer can always look it up, and the record says which earlier execution actually produced it.

The alternative to marking reuse is to return the recorded outcome as if it were fresh, which makes a cache hit
indistinguishable from a run that never happened. That is the failure the second key level exists to prevent, and an
unmarked hit reintroduces it one level higher.

## How a hit is recorded

A result hit is a real execution for record purposes. It receives a fresh execution identifier, a record is written
with status `running` and then with the terminal status its outcome implies, and the record carries the marker. The
response's `executionId` is this run's identifier and the marker's `executionId` is the producing run's.

Returning the producing run's identifier as this run's identifier was rejected: a lookup by it would answer with a
different execution's record, which is the one thing an identifier must never do.

## What is not stored

| Case | Stored as a result | Consequence |
| --- | --- | --- |
| a submission that did not compile | no | it recompiles on every invocation |
| a submission whose deadline expired | no | it runs again next time, because a killed run is not an answer |
| an invocation that bypassed the cache | no, and nothing is read | nothing is recorded and nothing is returned from the store |
| a refused request | no | nothing was executed, so there is nothing to record |

A compile failure stores nothing because no artefact ever existed and no execution happened. A deadline-expired run
stores nothing because its outcome is a decision about a time budget rather than a result of the program.

## Limitations

1. Nothing prunes either store. Artefacts and results accumulate for the life of the data root, and no size, age or use
   count removes them.
2. A stored result records the whole outcome, so its size follows the submission's output. A submission that prints a
   hundred megabytes leaves a hundred megabyte document, and every later hit reads it.
3. A stored result's timing fields are returned unfiltered. The burden of excluding them falls on the reader, guided by
   the marker. Zeroing them would discard the producing run's measurement and would make a hit indistinguishable from a
   submission that reported nothing.
4. A compile failure is never cached, so a submission that does not compile pays the full compile cost on every
   invocation, however many times it is resubmitted unchanged.
5. The artefact read is a check that one file exists. A directory whose assembly is present but whose runtime
   configuration or reference copies were deleted reads as a hit, and the child then fails at startup with an error
   that names the missing runtime configuration rather than the damaged store. Nothing verifies the contents of a stored
   artefact.
6. The artefact store write fails if the key is already present. Correctness therefore rests on the invocation lock: two
   executions cannot race to store one artefact because they cannot run at once. The store itself is not idempotent.
7. The artefact key carries no runtime identifier, so a store cannot hold two artefacts for one source that differ only
   by runtime identifier.
8. A result hit reports the producing execution's timings, and there is no field reporting how much time the hit itself
   saved.
9. Bypassing the cache is all-or-nothing. A caller cannot request compile reuse while refusing result reuse, so it
   cannot re-measure an unchanged submission without recompiling it.

## Acceptance criteria

1. A first execution of a submission creates one artefact directory under its artefact key, containing the assembly,
   its runtime configuration, and a copy of every supplied reference assembly.
2. A second execution of the same source with one parameter value changed reports `artefactReused` true,
   `resultReused` false, and compiles nothing.
3. A second execution identical to the first reports `resultReused` true, starts no child process, and returns the
   recorded outcome.
4. A reuse response carries the producing execution's identifier, recorded time and build version in the marker, and
   the marker's hint states that the timings are not this run's.
5. The record of a reuse names the producing execution in `reused`, and its own `executionId` is distinct from it.
6. A lookup by the reusing execution's identifier returns the reusing record, not the producing one.
7. An outcome recorded by a different build version is not returned; the request is compiled and executed again.
8. A submission whose deadline expired is not stored, and an identical resubmission compiles and executes again.
9. A submission that failed to compile stores no artefact and no result, and leaves the result store unchanged.
10. A bypassed cache reads nothing and writes nothing: the result store's contents are identical before and after.
11. An artefact store write whose rename is denied three times and then allowed succeeds, and the artefact reads back.
12. An artefact store write whose rename is denied every time raises an error after exactly 6 attempts and leaves no
    partial directory under the key.
13. Editing the source produces a miss at both levels and a new artefact directory, with the earlier directory untouched.
14. Changing a package version produces a miss at the artefact level and a new artefact directory.
