# The `run_csharp` contract

## Arguments

| Argument | Type | Required | Default |
| --- | --- | --- | --- |
| `source` | string | yes | none |
| `parameters` | object of string to string | no | `{}` |
| `timeoutSeconds` | integer | no | 60, clamped to 600 |

`source` is the whole compilation unit: raw C# text, not a path, not a fragment to be wrapped.

## Entry point and parameters

- The submission is one source file compiled as a console application.
- `static int Main(string[] args)` on any type. Its return value is the exit status.
- Parameters arrive as one `key=value` command-line argument per entry, sorted by key with an
  ordinal comparison. Everything after the first `=` is the value.
- The reference set is the .NET shared framework. An assembly outside it is a compile error.

## Result

One JSON object:

| Field | Meaning |
| --- | --- |
| `executionId` | 32 hex characters, present on every outcome. |
| `status` | `completed` when the submission ran and exited zero, otherwise `failed`. |
| `reused` | Null for a fresh execution; set when the outcome came from the cache. |
| `compile.success` | False when the compiler reported at least one error. |
| `compile.diagnosticCount` | Length of `compile.diagnostics`. |
| `compile.diagnostics` | `{ id, severity, message, filePath, line, column }` per error. |
| `run` | Null when the submission did not compile. |
| `run.exitStatus` | Null when the child never started or was killed at its deadline. |
| `run.standardOutput`, `run.standardError` | Everything written, including before a failure. |
| `run.deadlineExpired` | True only when the child was killed at its deadline. |
| `run.elapsedMilliseconds`, `run.processorMilliseconds`, `run.peakWorkingSetBytes` | Measurements of the child process. |
