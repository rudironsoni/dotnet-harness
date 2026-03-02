---
name: dotnet-agent-harness-hooks
description: |
  Guide for hook and MCP server integration patterns used by dotnet-agent-harness-compatible toolchains.
  Use when configuring Claude Code hooks (SessionStart/PostToolUse), aligning safety behaviors, or wiring MCP servers via `.mcp.json`.
  Keywords: hooks, PostToolUse, SessionStart, Claude Code hooks, MCP, .mcp.json, context injection, dotnet format hook
license: MIT
metadata:
  attribution:
    - Source: dotnet-artisan (Claire Novotny LLC) – hooks-and-mcp-guide.md (MIT)
    - Ported/adapted into dotnet-agent-harness
---

<!--
Attribution:
- Source: dotnet-artisan (Claire Novotny LLC) – docs/hooks-and-mcp-guide.md (MIT)
- This file was ported/adapted into dotnet-agent-harness.
-->

## Purpose

Provide a practical reference for:

- Hook behaviors that improve .NET workflows (formatting suggestions, restore prompts, basic validation).
- MCP server wiring and operational expectations.

## When to use

Use this skill when you need to:

- Configure or review hook behavior for a .NET repo.
- Add session-start context injection (solution/project discovery, TFM detection).
- Add or troubleshoot MCP servers configured via `.mcp.json`.
- Validate that hooks are non-blocking and fail-safe.

## Hook patterns

## PostToolUse hook (Write/Edit)

A common pattern is a single PostToolUse hook that matches `Write|Edit` and dispatches to a script (for example,
`scripts/hooks/post-edit-dotnet.sh`). The script can inspect the edited file extension and take appropriate action.

Example behaviors:

| File pattern            | Behavior                      | Notes                                                                            |
| ----------------------- | ----------------------------- | -------------------------------------------------------------------------------- |
| `*Tests.cs`, `*Test.cs` | Suggest running related tests | Emit a suggested `dotnet test --filter` targeting the modified test class        |
| `*.cs`                  | Auto-format                   | Run `dotnet format --include <file>` asynchronously and report results next turn |
| `*.csproj`              | Suggest restore               | Recommend `dotnet restore` after project file changes                            |
| `*.xaml`                | Validate XML well-formedness  | Use `xmllint` or fall back to `python3` XML parsing                              |

Safety properties for PostToolUse hooks:

- Run asynchronously when possible.
- Use a bounded timeout (for example, 60 seconds).
- Never block editing; exit successfully even on non-critical failures.

## SessionStart hook (startup)

A SessionStart hook can run once at session startup (for example, `scripts/hooks/session-start-context.sh`) to detect
whether the current directory appears to be a .NET project and inject context.

Common context signals:

- Count `.sln`/`.slnx` solution files (bounded depth, e.g., 3 levels).
- Count `.csproj` project files (bounded depth, e.g., 3 levels).
- Detect `global.json`.
- Extract a target framework moniker (TFM) from the first `.csproj` found.

Example injected context:

> This is a .NET project (net10.0) with 3 project(s) in 1 solution(s).

## MCP server configuration

MCP servers are commonly configured in `.mcp.json`.

## Example: Context7 (stdio via npx)

| Property  | Value                                                                       |
| --------- | --------------------------------------------------------------------------- |
| Transport | stdio                                                                       |
| Command   | `npx -y @upstash/context7-mcp@latest`                                       |
| Purpose   | Documentation lookup for Microsoft Learn, NuGet, and broader .NET ecosystem |

Operational notes:

- Using `@latest` is convenient for initial shipping; pin versions once stabilized to reduce upstream break risk.
- Node.js is required for MCP servers started via `npx`.

## Safety considerations

- Hooks should fail safe: if required tools are missing (`dotnet`, `jq`, `npx`, `xmllint`, `python3`), emit a warning
  and exit successfully.
- Hooks should be scoped: only act on files that match clear patterns.
- Avoid emitting or persisting secrets in hook output.

## Troubleshooting

## `dotnet` not found

If `dotnet` is not available in `PATH`, a formatting hook should degrade gracefully (warn and exit).

## `npx` not available

If Node.js is missing, `npx`-based MCP servers will not start.

## Hooks not firing

Hooks are typically snapshotted at session start. Restart the tool to pick up hook changes.

## `jq` not found

If hook scripts parse JSON from stdin, they may require `jq`. If missing, the scripts should report and exit
successfully.

## XAML validation unavailable

If neither `xmllint` nor `python3` are available, skip validation and report a warning.
