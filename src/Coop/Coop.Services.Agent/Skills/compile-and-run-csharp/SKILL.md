---
name: compile-and-run-csharp
description: Use when a question about C# or .NET behaviour is settled by executing a few lines rather than by reading a solution, and the answer must be exact.
allowed-tools: run_csharp
---

# Compile and run C#

`run_csharp` compiles one C# submission and runs it in a contained child process under a deadline.
Use it to answer a behaviour question by executing it instead of predicting the answer.

## When to use it

Does this compile. What does this print. What does this throw. What does this string or LINQ
operation return. What does this version of a library do. Anything where running a few lines is
cheaper and more certain than reading.

## When not to use it

- The answer is already in the source in front of you.
- The code needs credentials, a network service, a database or host state the execution does not have.
- The question needs a project reference or a full solution build; a submission is one source file
  against the shared framework.
- The work would exceed the deadline.
- The caller needs a measurement with attribution, which one run cannot give.

It is not free: a cold compile is hundreds of milliseconds and a cold package resolve is seconds.
Repeated identical submissions are reused, and the result says so.

It is not a security sandbox for hostile code. Use it for code you or your caller already trust.

## How to submit

- One source file, one entry point: `static int Main(string[] args)`. What `Main` returns is the
  exit status.
- Pass values in `parameters`, not stitched into the source.
- Set `timeoutSeconds` when the code may run long; the default applies otherwise.

## How to read the result

- `compile.diagnostics` answers a question too: a submission that fails to compile has told you
  something. Correct it once; a loop of compiles is what the deadline exists to stop.
- `run.deadlineExpired` true is not a pass.
- `reused` non-null means the timings belong to an earlier execution of the same source.

## What is recorded

The source text is stored verbatim in the execution record. Secrets, credentials and personal data
do not go into a submission.

The argument list, the entry point form, the parameter convention and the result fields are in the
`references/run-csharp-contract.md` resource.
