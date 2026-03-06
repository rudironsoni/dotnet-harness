---
description:
  'Bootstrap dotnet-agent-harness as a local .NET tool, write RuleSync config, and initialize target-specific agent
  outputs.'
targets: ['*']
---

# /dotnet-agent-harness:bootstrap

Bootstrap the runtime harness for OpenCode, Claude Code, Codex CLI, Gemini CLI, GitHub Copilot CLI, and Antigravity.

## Execution Contract

```bash
dotnet agent-harness bootstrap --targets claudecode,opencode,codexcli,geminicli,copilot,antigravity --run-rulesync
```

## What It Does

1. writes or updates `.config/dotnet-tools.json` for the local `dotnet-agent-harness` tool
2. writes `rulesync.jsonc` with the selected targets and toolkit source
3. writes repo-local state under `.dotnet-agent-harness/`
4. optionally runs `rulesync install` and `rulesync generate`
5. reports which platform output roots will be generated

## Options

- `--targets <csv>`: platform list. Supported ids: `claudecode`, `opencode`, `codexcli`, `geminicli`, `copilot`,
  `antigravity`
- `--features <csv>`: RuleSync features to generate. Defaults to `*`
- `--source <owner/repo>`: RuleSync source repository. Defaults to `rudironsoni/dotnet-agent-harness`
- `--source-path <path>`: install path for the RuleSync source. Defaults to `.rulesync`
- `--tool-version <x.y.z>`: pin a specific local tool version in `.config/dotnet-tools.json`
- `--run-rulesync`: run `rulesync install` and `rulesync generate` after writing config
- `--force`: overwrite an existing `rulesync.jsonc`
- `--no-save`: skip writing `.dotnet-agent-harness/` state files

## Notes

- Use this in consumer repositories where you want the local runtime and generated platform files to stay in sync.
- If RuleSync is not installed, bootstrap still writes the repo contract and tells you what to run next.
- Prefer this over ad hoc per-platform setup when the repository is meant to support multiple agent runtimes.
