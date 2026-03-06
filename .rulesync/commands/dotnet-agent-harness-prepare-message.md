---
description:
  'Assemble an enriched agent request from repository context, persona rules, recommended skills, and target resolution.'
targets: ['*']
---

# /dotnet-agent-harness:prepare-message

Prepare a repository-aware agent request bundle before implementation, review, or planning.

## Usage

```bash
/dotnet-agent-harness:prepare-message <request> [options]
```

## Options

- `--persona <id>`: Force a specific persona (`architect`, `reviewer`, `implementer`, `tester`)
- `--target <path>`: Resolve the request against a specific project or solution
- `--platform <id>`: Render the bundle for `generic`, `codexcli`, `claudecode`, `opencode`, or `copilot`
- `--limit <n>`: Limit the number of recommended skills included in the bundle
- `--write-evidence`: Persist the full prepared-message report and rendered prompt under
  `.dotnet-agent-harness/evidence/prepared-messages/`
- `--evidence-id <id>`: Use a stable artifact id when writing evidence so prompt bundles can be diffed across CI or
  incidents
- `--format prompt`: Emit only the rendered platform prompt instead of the full report

## Output

The prepared bundle includes:

1. resolved persona
2. resolved repository target or ambiguity warning
3. recommended skills and preferred subagent
4. enriched request text
5. four-layer prompt bundle:
   - system
   - tool
   - skill
   - request
6. rendered prompt messages for the selected agent platform
7. optional evidence artifacts for report JSON and rendered prompt text

## Notes

- Use this before implementation when the user request is vague or spans multiple projects.
- Use this before review to force findings-first behavior via the `reviewer` persona.
- Use this before architecture work to bind the request to repository shape and constraints.
- Persona `requestDirectives` are applied to the request layer so the rendered prompt carries persona-specific task
  framing.
