# Locking

Coop compiles into shared build output and executes children that consume the machine. Two invocations doing that at
once corrupt each other's output, so every invocation of the execution path takes one lock and holds it for the whole
job. This document defines what the lock is, what it covers, how long a caller waits, and the refusal it gets when the
wait runs out.

## What the lock is

The lock is an open file handle with no sharing, taken on one file at a fixed location under the data root. A holder is
a process that has the handle open. It is not a marker file and not a protocol over file content.

| Property | Behaviour |
| --- | --- |
| acquisition | the file is opened for read and write, created if absent, demanding exclusive access |
| contention | an open that fails because another holder has the file is retried |
| retry interval | 250 milliseconds |
| wait budget | 120 seconds from the first attempt |
| release | the handle is released when the holder finishes, and by the operating system when the holder process ends for any reason |
| presence on disk | a lock file that exists means nothing. Ownership is the handle, so a file left behind by a killed holder blocks nobody |
| deletion | the file is never deleted. Deleting it would let a second caller take a different file under the same name |

Because release is a property of the handle, a caller that is killed, crashes or is terminated releases the lock. There
is no stale-lock recovery, no owner file to inspect and no expiry to configure.

## Scope

The lock is per data root, not per user, per submission or per artefact. One root has one lock file.

Every surface that executes a submission takes the same lock: the command surface, the watched-folder consumer and the
network surface. A submission from one of them can never overlap a submission from another under the same root. Two
separate data roots have separate locks and do not exclude each other.

The held span covers, in order:

1. package resolution, including any network fetch
2. compilation, which writes into the shared staging area
3. the write of the artefact into the content-keyed store
4. the child process, from start to exit
5. the record write and any result write

Nothing inside the span takes a second lock, so no ordering exists between locks and no deadlock between them is
possible.

What the lock protects is the shared write targets: the staging area compilation writes into, the artefact store whose
contents are renamed into place, the package cache and the derived index. What it does not cover are the per-execution
documents and per-key result documents: those are distinct files per execution and per key, so concurrent writers never
address the same file and require no lock.

## Wait budget

A caller that arrives while the lock is held waits up to 120 seconds, polling every 250 milliseconds, and then takes the
lock as soon as it is released. Waiting is not an error and produces no output of its own.

The budget is a constant of the component. It is not part of a request, not read from configuration, and not varied by
a surface.

## Refusal when the lock cannot be acquired

When the budget expires the invocation is refused, and the refusal is reported as its own condition rather than as a
failed execution.

| Property | Behaviour |
| --- | --- |
| message | names the lock location and the number of seconds waited |
| command exit code | 3, distinct from 1 (no measured exit status), 2 (unusable command line), 4 (unresolvable data root), 5 (refused package) and 6 (unknown execution identifier) |
| what ran | nothing. No package was resolved, nothing was compiled, no child started |
| record | none is written by the command surface |

The refusal must be distinguishable from an execution failure because the two demand opposite responses. A lock
refusal says the request was fine and the machine was busy; the caller retries later, or waits longer, and the
submission is not at fault. A failed outcome says the submission ran and did not succeed. A caller that cannot tell
them apart either deletes a working submission or retries a submission that will fail identically. It is also the only
condition in the execution path whose cause lies outside the request entirely.

## Limitations

1. The lock serialises every execution under one data root. There is no queue, no fairness and no priority: a caller
   that arrives during a long submission waits and may be refused, and the caller that has been waiting longest is not
   served first. Ordering among waiting callers follows whichever retry wins.
2. A submission may run for up to 600 seconds. Any caller that arrives after a long submission starts will exhaust the
   120 second budget and be refused before that submission finishes. The two constants are independent, so a single
   long submission denies service to everyone else for its whole duration.
3. Package resolution happens inside the lock, so a slow or unreachable feed lengthens every other caller's wait.
4. The wait budget is not adjustable per request or per surface.
5. A refused caller learns only that the wait expired. Nothing reports whether the holder is alive, what it is
   executing, or how much of its work remains.
6. A surface that acknowledges a request before executing it does not refuse the caller: the refusal is recorded after
   the acknowledgement as a failed execution whose `standardError` carries the message and whose status is `failed`.
   The same condition therefore has two presentations, and only the command surface reports it as a refusal.
7. Because the file is never deleted, an operator inspecting the data root sees a lock file that is held only while
   some process has it open. The file's existence carries no information.

## Acceptance criteria

1. Two invocations started at the same moment against one data root both complete, neither corrupting the other's
   artefact, and the second observes the first's artefact as already present or compiles its own.
2. An invocation started while another holds the lock succeeds after the holder releases it, with no refusal, when the
   hold is shorter than the budget.
3. An invocation that cannot take the lock within 120 seconds is refused with a message naming the lock location and the
   budget, and exits 3.
4. A refused invocation compiles nothing, resolves no package and starts no child process.
5. Killing the holder process releases the lock, and the next invocation takes it without operator action.
6. A lock file that exists with no holder does not prevent an invocation from taking the lock.
7. Two invocations against two separate data roots proceed at the same time.
8. Every surface that executes a submission takes the same lock, so a submission from one surface cannot overlap a
   submission from another under one data root.
9. No invocation of the execution path holds a second lock while holding this one.
