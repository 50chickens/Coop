# Execution records

Every execution leaves one record, and the record outlives the process that produced it. One execution is one JSON
document on disk named for its execution id, so concurrent executions never write the same file and two executions
cannot overwrite each other's result. A derived, non-authoritative index over the documents answers queries without
reading every document.

## The record

A record carries the eight request fields plus the identity and state of the execution.

| Field | Type | Meaning |
| --- | --- | --- |
| `executionId` | string | identity of the execution, and the name of its document |
| `name` | string | from the request |
| `description` | string | from the request |
| `category` | string | from the request |
| `timestamp` | string | from the request, ISO-8601 with offset |
| `submission` | object | the code that was submitted |
| `shouldRunAsync` | boolean | from the request |
| `packages` | array | the packages the request declared |
| `response` | object or null | the outcome, once one exists |
| `status` | string | `pending`, `running`, `completed` or `failed` |
| `reused` | object or null | the marker naming the execution whose outcome this one returned, when the outcome was reused |

Document property names are camelCase, and an absent member deserialises to its default rather than failing the read, so
a document written by an older build still reads.

## Lifecycle

A record is written before anything the caller could want to know about happens, and rewritten as the execution
progresses.

| Moment | Status written |
| --- | --- |
| a surface acknowledges a request before executing it | `pending`, before the acknowledgement |
| execution begins | `running` |
| an outcome exists | `completed` or `failed` |

The terminal status is derived mechanically: `failed` when the deadline expired or the exit status is not zero,
`completed` otherwise. A submission that failed to compile is `failed`. A reused outcome takes the terminal status its
recorded outcome implies.

Because the record exists before the child starts, a caller that never receives a response can still find out what
happened, and a caller that was told an execution was accepted can poll for it from the moment the identifier is
returned.

## One document per execution

Each execution writes exactly one document, at a path derived from its execution id and nothing else. Two properties
follow and both are load bearing:

1. Two concurrent executions write two different files, so no write blocks, corrupts or overwrites another, and no
   coordination is needed beyond distinct identifiers.
2. The document is the record. Nothing else has to be consulted to answer what happened in that execution, and there is
   no shared file whose loss would lose more than one execution.

An execution id must be usable as a file name. An identifier carrying a path separator, a character the platform
forbids in a name, or a `..` segment is rejected before any read or write, so an identifier from a client cannot address
a document outside the store.

## How a document is written

A write never edits a document in place. The content is written to a temporary name unique to that write in the same
folder, and then renamed over the destination. Three consequences:

1. A reader never observes a half-written document. It sees the previous document or the new one.
2. A failed write leaves the previous document intact and a stray temporary file, rather than a truncated document.
3. The rename can be denied for an instant by something outside the process. It is retried up to 6 times, 50
   milliseconds apart. After the last attempt the denial is raised as an error; there is no silent success and no
   unbounded wait.

The same bounded retry is used wherever a rename or a move is the final step of a write.

## The derived index

The index is one document holding one entry per execution, so a query does not read every document.

| Index field | Meaning |
| --- | --- |
| `generatedAt` | when this index document was built |
| `documentCount` | how many execution documents existed when it was built |
| `documentSignature` | the sum, over the documents, of last-write ticks plus file length |
| `entries` | one entry per document, ordered by timestamp then by execution id |

| Entry field | Meaning |
| --- | --- |
| `executionId` | identity of the execution |
| `fileName` | the document this entry was read from |
| `name`, `description`, `category`, `timestamp`, `status` | copied from the document |
| `tokens` | the tokenised text of name, description and category, sorted |

Tokens are the lowercase runs of letters and digits, at least two characters long, surviving a fixed stop-word list.
Splitting is on every non-alphanumeric character, so `Newtonsoft.Json` contributes `newtonsoft` and `json`. Package ids
are not part of the token set: two executions that share a dependency but have unrelated purposes must not rank as
similar, and a package list is part of what makes two executions the same for result reuse, which is a different
question from what makes their prose similar.

## Why the index is downstream only

The index is derived and never authoritative. Two rules enforce that and they are not stylistic.

A writer of an execution writes only its own document. It does not rewrite the index. One shared index file rewritten
by every execution would reintroduce exactly the contention that per-execution document names exist to remove: every
concurrent execution would serialise on one file, and a write to it would be the single point where two executions
could lose each other's work. The writer does invalidate the index cached in its own process, which costs nothing and
cannot affect anyone else.

A reader refreshes the index. It rebuilds when the index is absent, when it does not account for the documents that are
present, or when the documents have changed since it was built. The checks are: the recorded document count differs
from the number of documents; an entry names a document that is absent; a document is present that no entry names; or
the signature sum differs.

Every answer is read from a document, never from an entry. An entry supplies the identifier and the ordering; the
document supplies everything returned. A record whose document has been deleted is therefore never returned, whatever
the index says, and the index converges on the next refresh.

This is what makes the index safe to lose, safe to rebuild and safe to be wrong about. The cost is one rebuild that
reads every document, paid by the reader that discovers the staleness.

## Queries

| Query | Behaviour |
| --- | --- |
| by execution id | reads that one document. An unknown identifier is refused by name rather than answered with an empty result |
| by category | entries whose category matches case-insensitively, returned newest first, then by execution id |
| by similarity | the count of shared tokens between the query text and an entry's tokens, ranked by score, then newest first, then by execution id, limited to the number requested |

An unknown execution identifier is refused with a message naming the identifier and the store it was looked in, on the
command surface with exit code 6, on a network surface as a not-found naming the identifier. It must be refused by name
rather than answered with an empty result because an empty result is indistinguishable from an execution that produced
no output, and a caller polling for a result would wait forever on an identifier that will never resolve.

## Refusals

| Condition | Reason it must be distinguishable |
| --- | --- |
| unknown execution identifier | an empty or null answer would make a mistyped identifier look like an execution with no result, and a caller polling would never learn it is polling nothing |
| identifier unusable as a file name | the identifier came from outside and cannot be allowed to address anything but a document of its own; refusing names the offending identifier instead of failing on a path |
| a document write whose rename is denied after every attempt | a lost record is not a lost result; the caller must be told the write failed rather than be told the execution did not happen |

## Limitations

1. The similarity query exists in the store and is exercised by tests, but no client surface exposes it. A client can
   query by identifier and by category only. Requirement-level intent for a similarity search is therefore implemented
   below the surface and unreachable above it.
2. Staleness is detected by a count, a set comparison and a sum of last-write ticks plus length. An edit that keeps a
   document's length and lands in the same timestamp tick as the index build is invisible, and two complementary edits
   can leave the sum unchanged. Both are unlikely and both are accepted rather than paid for with a hash of every
   document on every query.
3. Two readers that both find the index stale can both rebuild and write it. The result is last-writer-wins on a derived
   document, which is safe, and it means the index is not a serialised resource.
4. A refused request leaves no record, because nothing was executed. A client that wrote a request and had it refused
   must look where its surface reports refusals; the store cannot tell it what happened.
5. A record's `packages` field carries the declared entries, not the resolved closure, so a reader cannot reconstruct
   which transitive versions were compiled against.
6. A record is written twice: once before execution and once with the outcome. A process that ends between the two
   leaves a record in `running` for ever. Nothing reaps it and nothing can tell it apart from an execution still in
   flight other than by its age.
7. Nothing prunes records. A store grows by one document per execution for as long as the product is used, and every
   rebuild of the index reads all of them.
8. The similarity score counts shared tokens with no weighting, so a long description outranks a precise one by
   accident of length, and a query of one common word matches everything. The limit truncates rather than filters, so a
   caller cannot ask for "matching at least n".

## Acceptance criteria

1. Executing one request produces exactly one document, and its name is the execution id.
2. Two executions started at the same time produce two documents and neither is lost or truncated.
3. A record exists and reads back with status `running` after the execution begins and before the outcome is written.
4. The terminal status is `failed` exactly when the deadline expired or the exit status is not zero.
5. Reading a record by an identifier that exists returns the full request fields and its outcome.
6. Reading a record by an identifier that does not exist is refused with a message naming the identifier.
7. An identifier containing a path separator, a forbidden character or a `..` segment is refused before any read or
   write.
8. A query by category returns only records of that category, newest first.
9. Deleting a document causes its identifier to be absent from every query result after the next index refresh, and no
   answer is built from an entry alone.
10. Deleting the index document causes the next query to rebuild it and return the same answers.
11. Adding a document without touching the index causes the next query to include it after a refresh.
12. A document write whose rename is denied three times and then allowed succeeds, and the document reads back complete.
13. A document write whose rename is denied every time raises an error after exactly 6 attempts and leaves the previous
    document readable.
14. Two executions with unrelated purposes that share one package id are not ranked as similar by the similarity query.
15. A record carries the `reused` marker exactly when its outcome was returned from a stored result, and the marker
    names the execution that produced it.
