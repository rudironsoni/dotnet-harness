---
description: 'Run authored skill test cases through the local runtime.'
targets: ['*']
---

# /dotnet-agent-harness:test

Run the local skill-test harness instead of interpreting `test-cases/` manually.

## Execution Contract

Run:

```bash
dotnet agent-harness test [skill-name|all] [--format text|json|junit] [--filter value] [--fail-fast] [--output path]
```

## Notes

- `all` runs the entire authored skill suite
- `--format junit` is intended for CI systems
- `--filter` narrows test selection to matching case names
- `--fail-fast` stops on the first failing check

## Example

```bash
dotnet agent-harness test dotnet-agent-harness-test-framework --format json
```
