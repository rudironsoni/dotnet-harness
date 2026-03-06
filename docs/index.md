---
layout: home

hero:
  name: 'dotnet-agent-harness'
  text: 'Comprehensive .NET Development Toolkit'
  tagline:
    189 skills, 15 subagents, 20 commands, and a runtime harness for modern C#, ASP.NET Core, MAUI, Blazor, and
    cloud-native apps
  image:
    src: /logo.png
    alt: dotnet-agent-harness
  actions:
    - theme: brand
      text: Get Started
      link: /guide/installation
    - theme: alt
      text: View Skills
      link: /skills/
    - theme: alt
      text: GitHub
      link: https://github.com/rudironsoni/dotnet-agent-harness

features:
  - icon: 🎯
    title: 189 Specialized Skills
    details: Deep expertise across the entire .NET ecosystem, from C# coding standards to cloud deployment patterns.

  - icon: 🤖
    title: 15 AI Subagents
    details: Specialized agents for MAUI, Blazor, Security, Performance, and more - each with focused tool profiles.

  - icon: 🛠️
    title: 20 Commands and Runtime Workflows
    details: Slash commands plus runtime CLI workflows for prompt assembly, incidents, validation, and eval automation.

  - icon: 🔌
    title: MCP Integration
    details: Seamless integration with Serena, Context7, Microsoft Learn, GitHub, DeepWiki, and WinDbg MCP servers.

  - icon: 🐳
    title: CI and Release Automation
    details: Eval artifact emission, prompt evidence capture, and incident creation for nightly and release workflows.

  - icon: 🔍
    title: Semantic Search
    details: Find the right skill instantly with AI-powered semantic search across descriptions and tags.
---

## Quick Start

```bash
# Install the runtime tool
dotnet new tool-manifest
dotnet tool install Rudironsoni.DotNetAgentHarness

# Bootstrap agent targets in the repo
dotnet agent-harness bootstrap \
  --targets claudecode,opencode,codexcli,geminicli,copilot,antigravity \
  --run-rulesync
```

## Maintainer Runtime

```bash
# Build the runtime CLI and eval runner
dotnet build src/DotNetAgentHarness.Tools/DotNetAgentHarness.Tools.csproj
dotnet build src/DotNetAgentHarness.Evals/DotNetAgentHarness.Evals.csproj

# Pack the runtime as a .NET tool
dotnet pack src/DotNetAgentHarness.Tools/DotNetAgentHarness.Tools.csproj

# Prepare a repository-aware prompt bundle
dotnet agent-harness \
  prepare-message "Review the validation pipeline" \
  --target src/DotNetAgentHarness.Tools/DotNetAgentHarness.Tools.csproj \
  --platform codexcli \
  --write-evidence \
  --evidence-id review-validation

# Diff prompt bundles or capture incidents
dotnet agent-harness \
  compare-prompts review-validation review-validation-v2
```

## Platform Support

<div class="platforms">

| Platform       | Status             | Primary Agent      |
| -------------- | ------------------ | ------------------ |
| Claude Code    | ✅ Fully Supported | `dotnet-architect` |
| OpenCode       | ✅ Fully Supported | `dotnet-architect` |
| GitHub Copilot | ✅ Fully Supported | All agents         |
| Codex CLI      | ✅ Fully Supported | All agents         |
| Gemini CLI     | ✅ Fully Supported | All agents         |
| Antigravity    | ✅ Fully Supported | All agents         |

</div>

## Architecture Overview

```mermaid
graph TB
    subgraph "dotnet-agent-harness Toolkit"
        SKILLS[189 Skills]
        SUBAGENTS[15 Subagents]
        COMMANDS[20 Commands]
        MCP[6 MCP Servers]
        RUNTIME[Runtime CLI]
    end

    subgraph "Distribution"
        CLAUDE[.claude/]
        OPENCODE[.opencode/]
        COPILOT[.github/]
        CODEX[.codex/]
        GEMINI[.gemini/]
        ANTIGRAVITY[.agent/]
    end

    SKILLS --> CLAUDE
    SKILLS --> OPENCODE
    SKILLS --> COPILOT
    SKILLS --> CODEX
    SKILLS --> GEMINI
    SKILLS --> ANTIGRAVITY
    SUBAGENTS --> CLAUDE
    SUBAGENTS --> OPENCODE
    COMMANDS --> CLAUDE
    COMMANDS --> OPENCODE
    RUNTIME --> CODEX
    RUNTIME --> COPILOT
    RUNTIME --> GEMINI
    RUNTIME --> ANTIGRAVITY
```

## Latest Enhancements

### Runtime and Governance

- **Installable runtime**: `DotNetAgentHarness.Tools` now packs as a `.NET tool` with command `dotnet agent-harness`.
- **Bootstrap flow**: `bootstrap` writes the local tool manifest, `rulesync.jsonc`, repo state, and can generate all
  supported target outputs.
- **Runtime-backed commands**: generated `dotnet-agent-harness:*` command files are expected to call the local runtime
  instead of duplicating logic in prompt text.
- **Release workflow**: GitHub Actions now packs, smoke-installs, and publishes the tool package to GitHub Packages,
  with optional NuGet.org publication.
- **Prompt assembly**: `prepare-message` builds persona-aware prompt bundles from repo analysis, skills, and target
  resolution.
- **Prompt evidence**: prepared-message reports and rendered prompts can be persisted under
  `.dotnet-agent-harness/evidence/`.
- **Prompt diffing**: `compare-prompts` shows section-level changes across system, tool, skill, and request layers.
- **Incident tracking**: `incident add`, `from-eval`, `resolve`, and `close` link failures to prompt evidence and
  regression cases.
- **Eval artifacts**: the eval runner emits machine-readable artifacts that can be consumed by CI and governance
  workflows.
- **CI auto-linking**: `scripts/ci/run_evals.sh` can create incidents automatically when an eval artifact contains
  failed trials.

[View Full Changelog](/guide/changelog)
