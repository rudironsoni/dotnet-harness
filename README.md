# dotnet-agent-harness

> **The definitive .NET development companion for AI coding tools.**
>
> 189 specialized skills · 15 expert subagents · 20 powerful commands · Multi-platform distribution

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Workflows](https://github.com/rudironsoni/dotnet-agent-harness/workflows/CI/badge.svg)](https://github.com/rudironsoni/dotnet-agent-harness/actions)
[![RuleSync](https://img.shields.io/badge/Powered%20by-RuleSync-purple)](https://github.com/dyoshikawa/rulesync)

---

## Table of Contents

- [What is dotnet-agent-harness?](#what-is-dotnet-agent-harness)
- [Why RuleSync?](#why-rulesync)
- [Architecture & Components](#architecture--components)
- [Installation](#installation)
- [Usage](#usage)
- [Maintainer Runtime](#maintainer-runtime)
- [The Plugin Repository](#the-plugin-repository)
- [Contributing](#contributing)
- [License](#license)

---

## What is dotnet-agent-harness?

**dotnet-agent-harness** is a comprehensive, production-ready toolkit that transforms AI coding assistants into .NET
development experts. Unlike generic coding assistants, dotnet-agent-harness provides deep, specialized knowledge
covering the entire .NET ecosystem—from C# language features and ASP.NET Core patterns to MAUI mobile development and
cloud-native architecture.

### The Problem We Solve

When developers use AI coding tools for .NET development, they encounter:

- **Surface-level answers**: Generic responses that miss .NET-specific nuances
- **Outdated patterns**: Suggestions using deprecated APIs or anti-patterns
- **Inconsistent quality**: Varying expertise across different AI platforms
- **No standardization**: Each project reinvents guidance and conventions

**dotnet-agent-harness solves this** by providing a curated, version-controlled, continuously updated knowledge base
that ensures consistent, expert-level .NET guidance across all AI coding platforms.

### Serena MCP Integration

This toolkit now uses [Serena MCP](https://github.com/orgs/rudironsoni/dotnet-agent-harness/.rulesync/skills/serena) for
efficient code editing:

**Serena-First Approach:**

- **Symbol-level navigation** instead of text search
- **Precise refactoring** with automatic reference updates
- **Dependency analysis** via `serena_find_referencing_symbols`
- **Safe transformations** with symbol-aware operations

**Available in all subagents:**

- `serena_find_symbol` - Navigate to definitions
- `serena_get_symbols_overview` - Understand file structure
- `serena_find_referencing_symbols` - Track dependencies
- `serena_replace_symbol_body` - Precise edits

**Fallback:** If Serena unavailable, traditional Read/Grep/Edit tools work seamlessly.

**Learn more:**

- [dotnet-serena-code-navigation](.rulesync/skills/dotnet-serena-code-navigation/SKILL.md) - Navigation patterns
- [dotnet-serena-refactoring](.rulesync/skills/dotnet-serena-refactoring/SKILL.md) - Refactoring techniques
- [dotnet-serena-analysis-patterns](.rulesync/skills/dotnet-serena-analysis-patterns/SKILL.md) - Code analysis

### What You Get

| Component       | Count | Description                                                     |
| --------------- | ----- | --------------------------------------------------------------- |
| **Skills**      | 189   | Self-contained guidance documents covering specific .NET topics |
| **Subagents**   | 15    | Specialized AI agents with focused tool profiles                |
| **Commands**    | 20    | Slash commands for common workflows                             |
| **MCP Servers** | 6     | Model Context Protocol integrations                             |
| **Hooks**       | 3     | Automated session management                                    |

**Coverage Areas:**

- C# language features (modern patterns, nullable reference types, async/await)
- ASP.NET Core (Minimal APIs, MVC, Blazor, gRPC, SignalR)
- Data Access (EF Core, Dapper, caching strategies)
- Testing (xUnit, integration testing, mutation testing)
- Cloud-Native (Docker, Kubernetes, .NET Aspire)
- Mobile (MAUI, Uno Platform)
- Security (OWASP, authentication, authorization)
- Performance (profiling, optimization, memory management)

---

## Why RuleSync?

**RuleSync** is a declarative configuration system for distributing AI coding tool configurations. We chose RuleSync
because it solves a critical problem: **how do you maintain consistent, version-controlled AI tooling across multiple
platforms and projects?**

### The RuleSync Philosophy

```text
Source of Truth (.rulesync/) → Generation → Distribution → AI Platforms
```

Instead of manually copying files to each platform's configuration directory, RuleSync provides:

1. **Single Source of Truth**: All content lives in `.rulesync/` directory
2. **Platform Abstraction**: One configuration generates artifacts for Claude, OpenCode, Copilot, etc.
3. **Version Control**: Changes are tracked in Git with proper review processes
4. **Validation**: Built-in linting ensures consistency before distribution
5. **Distribution**: Automated sync to marketplace repositories

### Why This Matters

Without RuleSync, maintaining a multi-platform toolkit would require:

- **Manual file copying** across 7+ platform directories
- **Inconsistent updates** when one platform gets forgotten
- **No validation** until users encounter broken configurations
- **Difficult collaboration** with no clear contribution workflow

With RuleSync:

- **Edit once** in `.rulesync/skills/`, generate for all platforms
- **Validate before commit** with `just ci-rulesync`
- **Automated distribution** via GitHub Actions
- **Clear contribution model** through GitHub PRs

---

## Architecture & Components

Understanding the dotnet-agent-harness architecture helps you use it effectively and contribute meaningfully.

### Component Overview

```text
dotnet-agent-harness/
├── .rulesync/                    # Source of truth (you edit here)
│   ├── skills/                   # 189 knowledge modules
│   │   ├── dotnet-*/             # .NET-specific skills
│   │   ├── wiki-*/               # Documentation skills
│   │   └── harness-*/            # Toolkit meta-skills
│   ├── subagents/                # 15 specialized agents
│   │   ├── dotnet-architect.md   # Primary agent (OpenCode)
│   │   ├── dotnet-*-specialist/  # Domain experts
│   │   └── wiki-*/               # Documentation agents
│   ├── commands/                 # 20 slash commands
│   │   └── dotnet-agent-harness-*/     # Toolkit commands
│   ├── rules/                    # RuleSync configuration
│   │   ├── overview.md           # Entry point
│   │   └── 10-conventions.md     # Coding standards
│   ├── hooks.json                # Session automation
│   └── mcp.json                  # MCP server definitions
│
├── src/DotNetAgentHarness.Tools/ # Runtime CLI for analysis, prompts, incidents, validation
├── src/DotNetAgentHarness.Evals/ # Eval runner and artifact emission
├── .dotnet-agent-harness/        # Local evidence, eval artifacts, incident records (ignored)
├── docs/                         # VitePress documentation
├── packages/                     # NPM package sources
└── scripts/                      # Build automation
```

### Component Rationale

#### Skills (`.rulesync/skills/`)

**Purpose**: Self-contained, modular knowledge units.

**Why This Pattern**:

- **Granularity**: Each skill covers one specific topic deeply
- **Composability**: Skills reference each other (`[skill:dotnet-efcore-patterns]`)
- **Discoverability**: Clear naming convention (`dotnet-{domain}-{topic}`)
- **Testing**: Each skill can be validated independently

**Example Structure**:

```yaml
# .rulesync/skills/dotnet-efcore-patterns/SKILL.md
---
name: dotnet-efcore-patterns
description: 'Entity Framework Core patterns: repository, unit of work, query optimization'
targets: ['*']
tags: ['dotnet', 'skill', 'data/ef-core']
---
# Content with examples, best practices, anti-patterns
```

#### Subagents (`.rulesync/subagents/`)

**Purpose**: Specialized AI agents with constrained tool profiles for specific domains.

**Why This Pattern**:

- **Focus**: Each subagent is an expert in one domain (e.g., `dotnet-maui-specialist`)
- **Safety**: Tool profiles limit what agents can do (e.g., read-only vs. full access)
- **Performance**: Smaller context = faster responses
- **User Experience**: `@mention` to summon the right expert

**Tool Profiles**:

| Profile       | Allowed Tools       | Use Case                       |
| ------------- | ------------------- | ------------------------------ |
| **Read-only** | Read, Grep, Glob    | Research and analysis          |
| **Standard**  | + Bash (validation) | Testing and verification       |
| **Full**      | + Edit, Write       | Implementation and refactoring |

**Primary vs. Subagents**:

- **Primary agents** (e.g., `dotnet-architect`): Appear in OpenCode Tab cycling
- **Subagents**: Invoked via `@mention` for specific tasks

#### Commands (`.rulesync/commands/`)

**Purpose**: Reusable workflows invoked via slash commands.

**Why This Pattern**:

- **Consistency**: Same command works across all platforms
- **Documentation**: Commands are self-documenting
- **Testing**: Can be validated independently
- **Extensibility**: Easy to add new workflows

**Command Categories**:

| Command                                 | Purpose                                       |
| --------------------------------------- | --------------------------------------------- |
| `/dotnet-agent-harness:search`          | Find skills by keyword or semantic similarity |
| `/dotnet-agent-harness:prepare-message` | Assemble persona-aware prompt bundles         |
| `/dotnet-agent-harness:compare-prompts` | Diff saved prompt evidence                    |
| `/dotnet-agent-harness:incident`        | Track prompt and eval failures                |
| `/dotnet-agent-harness:test`            | Run automated skill tests                     |

#### Hooks (`.rulesync/hooks.json`)

**Purpose**: Automate session setup and tool usage.

**Why This Pattern**:

- **Context Injection**: Automatically detect .NET projects and inject relevant skills
- **Quality Gates**: Run `dotnet format` on file saves
- **Zero Configuration**: Works out of the box

**Hook Types**:

- `sessionStart`: Detect project type, suggest skills
- `postToolUse`: Format code, validate changes
- `beforeSubmitPrompt`: Inject reminders for .NET projects

#### MCP Servers (`.rulesync/mcp.json`)

**Purpose**: Integrate external tools via Model Context Protocol.

**Why This Pattern**:

- **Ecosystem**: Leverage existing tools (Serena, Context7, GitHub)
- **Standardization**: MCP is becoming the standard for AI tool integration
- **Extensibility**: Easy to add new integrations

**Current Integrations**:

- **Serena**: Semantic code analysis via LSP
- **Context7**: Up-to-date library documentation
- **Microsoft Learn**: Official Microsoft documentation
- **WinDbg MCP**: Crash and dump analysis
- **DeepWiki MCP**: Documentation retrieval and Q&A
- **GitHub MCP**: Repository operations

MCP inventory (source: `.rulesync/mcp.json`): `context7`, `deepwiki`, `github`, `mcp-windbg`, `microsoftdocs-mcp`,
`serena`.

---

## Installation

### Prerequisites

- Git
- just (task runner)
- .NET 10 SDK (for evaluation harness)
- GitHub CLI (optional, for `gh` commands)

### Quick Start (Project Mode - Recommended)

Project mode installs the full harness in your repository:

```bash
# 1. Install rulesync (binary)
curl -fsSL https://github.com/dyoshikawa/rulesync/releases/latest/download/install.sh | bash

# 2. Fetch the harness
rulesync fetch rudironsoni/dotnet-agent-harness:.rulesync

# 3. Generate for all platforms
rulesync generate --targets "*" --features "*"
```

**What This Creates**:

- `.claude/` - Claude Code configuration
- `.opencode/` - OpenCode configuration
- `.github/` - GitHub Copilot configuration
- `.codex/` - Codex CLI configuration
- `.gemini/` - Gemini CLI configuration
- `AGENTS.md` - Agent definitions

### Declarative Sources (Advanced)

For curated skill selection or multiple sources:

```bash
# 1. Create rulesync.jsonc
cat > rulesync.jsonc << 'EOF'
{
  "sources": [
    { "source": "rudironsoni/dotnet-agent-harness", "path": ".rulesync" }
  ],
  "targets": ["claudecode", "opencode"],
  "features": ["skills", "subagents", "commands"]
}
EOF

# 2. Install and generate
rulesync install
rulesync generate
```

### Docker (Containerized)

```bash
# Pull the image
docker pull ghcr.io/rudironsoni/dotnet-agent-harness:latest

# Run commands
docker run --rm -v $(pwd):/workspace \
  ghcr.io/rudironsoni/dotnet-agent-harness:latest \
  rulesync validate
```

### Platform-Specific Notes

**Claude Code**:

- Install to project root
- Primary agent `dotnet-architect` appears in agent list

**OpenCode**:

- Primary agent `dotnet-architect` cycles with Tab
- Subagents invoked via `@dotnet-maui-specialist`

**GitHub Copilot**:

- Configuration in `.github/`
- Subagents available via `@mention`

---

## Usage

### Finding Skills

```bash
# Search by keyword
/dotnet-agent-harness:search authentication

# Search by category
/dotnet-agent-harness:search --category ui/blazor

# Semantic search
/dotnet-agent-harness:search "how to handle JWT tokens"
```

### Using Subagents

```text
@dotnet-maui-specialist I need to implement platform-specific services for iOS and Android

@dotnet-security-reviewer Can you review this API controller for OWASP compliance?
```

### Preparing Agent Requests

Use the runtime CLI when you need a deterministic, repository-aware prompt bundle before implementation, review, or
planning:

```bash
dotnet run --project src/DotNetAgentHarness.Tools/DotNetAgentHarness.Tools.csproj -- \
  prepare-message "Review the runtime validation path" \
  --target src/DotNetAgentHarness.Tools/DotNetAgentHarness.Tools.csproj \
  --platform codexcli \
  --write-evidence \
  --evidence-id review-runtime-validation \
  --format prompt
```

This produces:

- persona selection (`reviewer`, `architect`, `implementer`, `tester`)
- target resolution against the repo profile
- recommended skills and preferred subagent
- a four-layer prompt bundle (`system`, `tool`, `skill`, `request`)
- optional evidence under `.dotnet-agent-harness/evidence/prepared-messages/`

### Comparing Prompt Bundles

```bash
dotnet run --project src/DotNetAgentHarness.Tools/DotNetAgentHarness.Tools.csproj -- \
  compare-prompts review-runtime-validation review-runtime-validation-v2 --format json
```

Use this before merging persona or tool-policy changes so prompt regressions are visible at the section level.

### Incident Tracking

Record prompt failures directly from saved evidence:

```bash
dotnet run --project src/DotNetAgentHarness.Tools/DotNetAgentHarness.Tools.csproj -- \
  incident add "Reviewer misrouted validation request" \
  --prompt-evidence review-runtime-validation \
  --severity high \
  --owner platform-team
```

Create incidents automatically from eval artifacts:

```bash
dotnet run --project src/DotNetAgentHarness.Tools/DotNetAgentHarness.Tools.csproj -- \
  incident from-eval nightly-routing \
  --prompt-evidence review-runtime-validation \
  --incident-id nightly-routing-failure \
  --owner eval-bot
```

Close the loop only after the failure has a permanent regression case:

```bash
dotnet run --project src/DotNetAgentHarness.Tools/DotNetAgentHarness.Tools.csproj -- \
  incident resolve nightly-routing-failure \
  --owner platform-team \
  --rationale "Added permanent regression coverage." \
  --regression-case routing-reviewer-001 \
  --regression-path tests/eval/cases/routing.yaml

dotnet run --project src/DotNetAgentHarness.Tools/DotNetAgentHarness.Tools.csproj -- \
  incident close nightly-routing-failure \
  --owner release-manager \
  --rationale "Regression remains green in nightly and release gates." \
  --regression-case routing-reviewer-001
```

### Running Tests

```bash
# Test a specific skill
/dotnet-agent-harness:test dotnet-efcore-patterns

# Test all skills
/dotnet-agent-harness:test --all

# Generate coverage report
/dotnet-agent-harness:test --coverage --output report.html
```

### Profiling Performance

```bash
# Analyze current session
/dotnet-agent-harness:profile

# Compare with baseline
/dotnet-agent-harness:profile --compare-with baseline.json
```

### Running Evals v2

```bash
# Deterministic local fixture mode (default safety mode)
just eval

# Real provider mode (requires EVAL_OPENAI_KEY)
just eval-real

# CI gate entrypoint (defaults: real mode + 3 trials)
just ci-eval
```

Notes:

- Dummy mode is intentionally blocked when `CI=true` to prevent false confidence in release gates.
- If provider credentials are not available yet, keep fixture mode for local/dev validation (`just eval`) and use
  `just build-eval` as a temporary CI signal until real-provider secrets are wired.
- Eval artifacts can be written to `.dotnet-agent-harness/evidence/evals/` with `--artifact-id` or `--artifact-path`.

### CI Incident Automation

The CI wrapper preserves the eval exit code and can auto-create incidents when a case fails:

```bash
DOTNET_AGENT_HARNESS_EVAL_CREATE_INCIDENT=true \
DOTNET_AGENT_HARNESS_EVAL_PROMPT_EVIDENCE_ID=review-runtime-validation \
DOTNET_AGENT_HARNESS_EVAL_ARTIFACT_ID=nightly-routing \
DOTNET_AGENT_HARNESS_EVAL_INCIDENT_ID=nightly-routing-failure \
DOTNET_AGENT_HARNESS_EVAL_INCIDENT_OWNER=ci-eval-bot \
bash scripts/ci/run_evals.sh --cases tests/eval/cases/routing.yaml
```

Supported CI environment variables:

- `DOTNET_AGENT_HARNESS_EVAL_GATE`
- `DOTNET_AGENT_HARNESS_EVAL_POLICY_PROFILE`
- `DOTNET_AGENT_HARNESS_EVAL_ARTIFACT_ID`
- `DOTNET_AGENT_HARNESS_EVAL_PROMPT_EVIDENCE_ID`
- `DOTNET_AGENT_HARNESS_EVAL_CREATE_INCIDENT`
- `DOTNET_AGENT_HARNESS_EVAL_INCIDENT_OWNER`
- `DOTNET_AGENT_HARNESS_EVAL_INCIDENT_SEVERITY`
- `DOTNET_AGENT_HARNESS_EVAL_INCIDENT_ID`
- `DOTNET_AGENT_HARNESS_EVAL_INCIDENT_NOTES`

`.dotnet-agent-harness/` is repo-local runtime state and is ignored by Git.

---

## Maintainer Runtime

The repository now ships two runtime executables for maintainers and CI:

```bash
# Toolkit runtime
dotnet build src/DotNetAgentHarness.Tools/DotNetAgentHarness.Tools.csproj

# Eval runtime
dotnet build src/DotNetAgentHarness.Evals/DotNetAgentHarness.Evals.csproj
```

Primary maintainer workflows:

```bash
# Analyze the current repo
dotnet run --project src/DotNetAgentHarness.Tools/DotNetAgentHarness.Tools.csproj -- analyze --format json

# Recommend relevant skills and subagents
dotnet run --project src/DotNetAgentHarness.Tools/DotNetAgentHarness.Tools.csproj -- recommend --format json

# Validate authored content and runtime checks
dotnet run --project src/DotNetAgentHarness.Tools/DotNetAgentHarness.Tools.csproj -- validate --mode all --format json

# Emit an eval artifact
dotnet run --project src/DotNetAgentHarness.Evals/DotNetAgentHarness.Evals.csproj -- \
  --cases tests/eval/cases/routing.yaml \
  --artifact-id local-routing-smoke
```

---

## The Plugin Repository

**Critical Understanding**: This repository (`rudironsoni/dotnet-agent-harness`) is the **source of truth**. The actual
distribution happens through a separate repository:

### Repository Structure

| Repository                    | Purpose          | Content                                |
| ----------------------------- | ---------------- | -------------------------------------- |
| `dotnet-agent-harness`        | **Source**       | `.rulesync/`, scripts, docs, workflows |
| `dotnet-agent-harness-plugin` | **Distribution** | Generated artifacts only               |

### Why Two Repositories?

1. **Separation of Concerns**: Source code vs. distribution artifacts
2. **Clean Git History**: Generated files don't clutter source repo
3. **Platform Abstraction**: Different platforms need different file layouts
4. **Marketplace Integration**: Plugin repo serves as marketplace surface

### Distribution Flow

```text
Source Repo (dotnet-agent-harness)
    ↓ Push to main
GitHub Actions
    ↓ Generate bundles
Distribution Repo (dotnet-agent-harness-plugin)
    ↓ Marketplace sync
AI Platforms (Claude, OpenCode, etc.)
```

### What Gets Distributed

**Source Repo** (this repo):

- `.rulesync/skills/*.md` → human-editable skills
- `.rulesync/subagents/*.md` → agent definitions
- `.rulesync/commands/*.md` → slash commands

**Plugin Repo** (`rudironsoni/dotnet-agent-harness-plugin`):

- `.claude/skills/*.md` → Claude-specific formatting
- `.opencode/skills/*.md` → OpenCode-specific formatting
- `.github/skills/*.md` → Copilot-specific formatting
- NPM packages → Platform-specific packages

### Accessing the Plugin Repo

```bash
# Users install from plugin repo
rulesync fetch rudironsoni/dotnet-agent-harness-plugin:.claude

# Or use platform-specific packages (coming soon)
# See https://github.com/rudironsoni/dotnet-agent-harness-plugin for distribution
```

### Governance

- **Humans edit only** in `dotnet-agent-harness`
- **Bot manages** `dotnet-agent-harness-plugin`
- **Pull requests** in source repo trigger automatic plugin updates
- **No manual edits** in plugin repo (enforced by CI)

---

## Contributing

We welcome contributions! This section explains how to contribute effectively.

### Development Setup

```bash
# 1. Fork and clone
git clone https://github.com/YOUR_USERNAME/dotnet-agent-harness.git
cd dotnet-agent-harness

# 2. Install just (task runner)
# macOS: brew install just
# Linux: curl --proto '=https' --tlsv1.2 -sSf https://just.systems/install.sh | bash
# Windows: winget install --id Casey.Just

# 3. Install dependencies
just setup

# 4. Run validation
just ci-rulesync
```

### Contribution Workflow

```bash
# 1. Create branch
git checkout -b feature/my-new-skill

# 2. Make changes to .rulesync/
vim .rulesync/skills/dotnet-my-new-skill/SKILL.md

# 3. Validate locally
just ci-rulesync

# 4. Commit
git add -A
git commit -m "Add skill: dotnet-my-new-skill

Description of what this skill does and why it's needed."

# 5. Push and PR
git push origin feature/my-new-skill
gh pr create --title "Add dotnet-my-new-skill skill"
```

### What to Contribute

**Skills** (Most Common):

- New .NET topics not yet covered
- Updates to existing skills for new .NET versions
- Corrections for outdated patterns
- Additional examples or clarifications

**Subagents**:

- New domain specialists (e.g., `dotnet-ml-specialist`)
- Tool profile improvements
- Bug fixes for agent behavior

**Commands**:

- New workflow automation
- Enhancements to existing commands
- Bug fixes

**Documentation**:

- README improvements
- VitePress docs enhancements
- Example tutorials

### Skill Writing Guidelines

**Structure**:

````markdown
---
name: dotnet-{domain}-{topic}
description: 'Clear, one-line description'
targets: ['*']
tags: ['dotnet', 'skill', 'category/subcategory']
---

# Skill Title

## Overview

Brief explanation of the topic.

## Key Concepts

- Point 1
- Point 2

## Examples

```csharp
// Good example
public void GoodExample() { }

// Bad example (with explanation)
public void BadExample() { } // Don't do this because...
```
````

## Cross-References

- [skill:dotnet-related-skill]

## Best Practices

1. Do this
2. Don't do that

**Requirements**:

- Must have frontmatter with `name`, `description`, `targets`
- Must pass `just lint-frontmatter`
- Must include code examples
- Must reference related skills
- Must be tagged with appropriate categories

### Code of Conduct

- Be respectful and constructive
- Focus on improving .NET development experience
- Welcome feedback and iterate on PRs
- Help newcomers learn the contribution process

### Review Process

1. **Automated Checks**: Lint, format, validation
2. **Maintainer Review**: Content accuracy, style consistency
3. **Community Feedback**: 48-hour window for comments
4. **Merge**: Squash and merge to main

---

## License

MIT License. See [LICENSE](LICENSE).

---

## Acknowledgments

- Original corpus: [dotnet-artisan](https://github.com/novotnyllc/dotnet-artisan)
- RuleSync framework: [rulesync](https://github.com/dyoshikawa/rulesync)
- Community contributors: Thank you!

---

<div align="center">

**[⬆ Back to Top](#dotnet-agent-harness)**

Built with ❤️ for the .NET community

</div>
```
