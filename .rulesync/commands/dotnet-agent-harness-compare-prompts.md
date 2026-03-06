---
description: 'Compare two saved prepared-message prompt bundles by system, tool, skill, and request sections.'
targets: ['*']
---

# /dotnet-agent-harness:compare-prompts

Compare prepared prompt evidence artifacts stored under `.dotnet-agent-harness/evidence/prepared-messages/`.

## Usage

```bash
/dotnet-agent-harness:compare-prompts <left-evidence-id> <right-evidence-id> [options]
```

## Options

- `--format <text|json>`: Choose human-readable or machine-readable output
- `--output <path>`: Save the comparison result

## Output

The comparison includes:

1. left and right evidence ids
2. persona, platform, and target metadata
3. per-section comparison for:
   - system
   - tool
   - skill
   - request
4. first differing line and left-only/right-only line samples

## Notes

- Compare prompt evidence ids produced by `prepare-message --write-evidence`.
- Use this to review prompt regressions before changing personas, tool policy, or request shaping.
