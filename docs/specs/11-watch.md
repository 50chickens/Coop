# 11 - The watched folder

## Purpose

Not every caller is a process that can hold a connection. A caller that would rather write a file
than call a tool writes its submission as a document into a directory, and the watcher consumes it.

The watcher is the cheapest client surface: no protocol, no listener, no client library. It has
three obligations and they are the whole of this document:

1. Take a submission exactly once, even when more than one watcher is running.
2. Move it to a terminal directory when it has an outcome, or to a refusal directory when it could
   not be accepted.
3. Write the outcome to the record store, so the caller collects a result by execution identifier
   whether it waited or not.

## The three directories

| Role | Contents | Created by |
| --- | --- | --- |
| Queue | Request documents awaiting a watcher. | The watcher at start, if absent. |
| Completed | One document per execution that reached an outcome. | The watcher at start, if absent. |
| Failed | The request document as the caller wrote it, plus a refusal document beside it. | The watcher at start, if absent. |

The three directories are children of one working root, which is resolved from configuration or
from an explicit argument at start. Nothing else is written to them, and the watcher never deletes
a document from the completed or failed directory.

## File naming

| Artifact | Name pattern | Notes |
| --- | --- | --- |
| Request, as written by a client | `<request name>.json` | `<request name>` is the client's choice; the request extension is fixed by configuration, `.json` by default. Only names ending in the request extension are considered. |
| Claim | `<request name>.json.<execution id>.claim` | Produced by the claim rename. It no longer ends in the request extension, so no watcher will consider it again. |
| Completed | `<execution id>.json` | The execution identifier is the name, so a caller that lost the claim line can still find its result. |
| Refused request | `<request name>.json` in the failed directory | The caller's own name is preserved, because the caller's next action is to correct and resubmit it. |
| Refusal document | `<request name>.json.error.json` in the failed directory | Written atomically: temporary name, then replace. |

Names are matched on the request extension only. A name that ends in any other extension, including
`.json.writing`, `.json.tmp` and `.json.<id>.claim`, is invisible to the poll.

## Claim by rename

Claiming is one rename, in place, within the queue directory.

1. Generate an execution identifier before claiming, so the identifier exists even for a request
   that is refused.
2. Rename the request to the claim name. This is the claim.
3. If the rename fails because the source no longer exists, another watcher took it. Say nothing,
   record nothing, move to the next candidate.

Properties this gives, and they are the reason for the design:

- Two watchers cannot execute the same request. The operating system removes the source name in the
  same operation that creates the claim name, and only one renamer can succeed.
- No lock file, no lease, no shared state. There is nothing to time out and nothing to clean up.
- The execution identifier is visible in the queue directory while the execution is still running,
  so an operator can correlate a stuck claim with an in-flight record.

## What a client must do

A client writes the request to a temporary name that does not end in the request extension, then
renames it to `<request name>.json`.

Without this step a watcher can read a half-written document and refuse a request that was only
half-written. The watcher must not defend against this with a settle delay, a size check or a
retry: the contract is the rename, and a client that skips it is outside the contract.

A document whose name does not end in the request extension is invisible to the poll, whatever its
content: a complete request body written under a temporary name is not consumed, and is consumed on
the first pass after it is renamed.

## Validation

A claimed request is validated against the execution template, not against a field list the
watcher carries.

- The document must parse as JSON. A parse failure is a refusal with field `request` and the
  parser's message.
- The document must be a JSON object. Anything else is a refusal with field `request`.
- Every field the template marks required must be present and non-null. The first absent one is the
  refusal, with the field named.
- `submission` must be an object, and every submission field the template marks required must be
  present. Its refusal field is `submission.<field>`.
- Every declared package must resolve. A range, a malformed version, an id absent from the feed, or
  a package with no assembly for the target framework is a refusal with field `packages`.

A required field added to the template therefore becomes required by the watcher with no code
change, which is the point.

## Outcome

Every request that is accepted has an outcome, including a submission that fails to compile or
exits non-zero. The outcome is not a refusal.

1. The execution is taken under the single-run lock shared with every other surface, around package
   resolution and execution. A lock that is not acquired within its budget refuses the request
   rather than producing an outcome.
2. The record is written as `running` before compilation, and as terminal with the response filled
   after. A caller polling the record sees progress.
3. The claim is renamed into the completed directory as `<execution id>.json`. The document's content
   is the request as the client wrote it; the outcome lives in the record store under the same
   identifier.

The execution's deadline is the template's default. A request has no per-request timeout field on
this surface, and the watcher does not invent one.

`shouldRunAsync` is accepted and ignored: the watcher is itself the asynchronous path, and the
record moves from `running` to a terminal status exactly as it does for a synchronous caller. A
client that needs to know when the execution finished polls the record; the presence of the
completed document is the cheaper signal that it is over.

## The refusal artifact

A request that cannot be accepted is moved to the failed directory under its own name, and a
refusal document is written beside it:

```json
{
  "executionId": "e5852e86abfd47babaa9e6de7c03ea0c",
  "requestFile": "<request name>.json",
  "field": "category",
  "reason": "category is required by the execution template and this request does not carry it"
}
```

| Field | Meaning |
| --- | --- |
| `executionId` | The identifier generated before the claim. It names no record, because nothing was recorded. |
| `requestFile` | The name the caller wrote. |
| `field` | The template field that failed, `request` for a parse failure, `packages` for an unresolved package, or `lock` for a lock refusal. |
| `reason` | One sentence, containing the field name and what was wrong with it. |

No record is written for a refused request. Nothing was executed, so there is nothing to record,
and a record asserting otherwise would be a false history entry. The refusal is found where the
caller put the request.

A second refusal of a request with the same name replaces the earlier refusal in the failed
directory. The failed directory is therefore a set of current refusals, not a log; the record store
is where the durable history lives.

## Running, polling and stopping

- The watcher enumerates the queue directory on each pass, in the order the filesystem returns
  entries. There is no sorting, no priority and no ordering guarantee between requests.
- A pass consumes every candidate it finds before the next delay.
- The delay between passes is 250 ms by default and is configuration, not a command-line argument.
- A single-pass mode processes what is present, then exits 0. It is the mode a pipeline uses.
- The process stops on interrupt after finishing the request in hand, and exits 0. A request
  claimed when the process dies is left as a claim; see the limitations.

Two watchers may run against one queue. The claim rename is the only coordination they need.

## Refusals

| Condition | Behaviour |
| --- | --- |
| The working root cannot be resolved | Message naming the argument to pass; exit 4; nothing consumed. |
| The request document is not JSON | Moved to failed, refusal document with field `request`. |
| A required request or submission field is absent | Moved to failed, refusal document naming the field. |
| A package is refused | Moved to failed, refusal document with field `packages`. |
| The claim rename fails because another watcher claimed first | Not an error. No output, no record, next candidate. |
| The single-run lock cannot be taken within 120 seconds | The request is refused rather than executed: the claim moves to the failed directory under the caller's own name, with a refusal document whose `field` is `lock` and whose reason names the wait. No record is written, because nothing ran. |

A refusal is per request. It never stops the watcher, and it never blocks the queue.

## Limitations

- A claim whose watcher died is never reclaimed. The claim no longer ends in the request extension,
  so no pass will consider it, and it stays in the queue directory until a human looks. This is
  deliberate: a retry policy needs a lease, a clock and a decision about at-most-once versus
  at-least-once, and none of those exists here. The execution identifier in the claim name is the
  diagnostic: the record for it is either absent (nothing ran) or `running` (it died mid-execution).
- A request is executed at most once. A caller that wants a retry resubmits, and a resubmission is a
  different execution with a different identifier.
- There is no priority, no deadline per request, and no way to cancel a request once claimed.
- The completed document is a copy of the request and carries no outcome. A caller that needs the
  outcome reads the record; the two are not the same document and cannot be reconciled if the
  record store is lost while the completed directory survives.
- The failed directory holds one refusal per name. Two different requests written under one name
  are indistinguishable in that directory after the second refusal.
- Consumption is serialized by the single-run lock, so a queue of N requests takes N execution
  times. There is no batching and no parallelism.
- The watcher must be running. There is no service registration, no restart policy and no
  supervision; a stopped watcher leaves the queue untouched and no caller is told.

## Acceptance criteria

1. A request written into the queue as `<name>.json` is consumed on the next pass: it is claimed,
   executed, and moved to the completed directory as `<execution id>.json`, and the queue is empty
   afterwards.
2. The record for the consumed request can be read by the identifier that names the completed
   document, and its `response` carries the standard output the submission wrote.
3. Two watcher passes over one queued request execute it once: one completed document, one record,
   and the second pass produces no output and no second record.
4. A request written to a temporary name that does not end in the request extension is not consumed,
   the file is left in place, and no record exists; after it is renamed to `<name>.json` it is
   consumed on the next pass.
5. A request that is not JSON is moved to the failed directory under its own name with a refusal
   document whose `field` is `request`.
6. A request missing any one required template field is refused with that field named, and the
   refusal document carries the request's name and the generated identifier.
7. A submission that fails to compile has an outcome: the claim moves to the completed directory and
   the record's status is `failed` with diagnostics present.
8. A submission whose package version is a range is refused with field `packages` before any
   compilation, and no record is written.
9. A request carrying `shouldRunAsync` true is executed by the watcher, and its record reaches a
   terminal status.
10. A watcher started with an unresolvable root exits 4 and consumes nothing.
11. The completed and failed directories are created when absent, and the watcher starts with an
    empty queue without error.
