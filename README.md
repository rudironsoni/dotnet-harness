# dotnet-agent-harness

> **The definitive .NET development companion for AI coding tools.**
>
> 193 specialized skills · 18 expert subagents · 28 powerful commands

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

---

## Use in a Repo

Install the toolkit in your .NET repository:

```bash
# Bootstrap your repo with agents
rulesync fetch rudironsoni/dotnet-agent-harness:.rulesync
rulesync generate --targets "claudecode,codexcli,opencode,geminicli,antigravity,copilot,factorydroid" --features "*"
```

That's it! The bootstrap command will install the appropriate agent configurations for your project.

## Available Commands

After installation, use these RuleSync commands:

- `rulesync generate` - Generate agent configurations
- `rulesync generate --check` - Validate generation is deterministic
- `rulesync install` - Install from declarative sources

## Documentation

- **[Consumer Guide](docs/guide/)** - Installation, daily commands, troubleshooting
- **[Maintainer Guide](docs/maintainer/)** - Authoring skills, bundle generation, releases

## What You Get

| Component       | Count | Description                                            |
| --------------- | ----- | ------------------------------------------------------ |
| **Skills**      | 193   | Self-contained guidance documents for .NET topics      |
| **Subagents**   | 18    | Specialized AI agents for specific domains             |
| **Commands**    | 28    | CLI commands for common workflows                      |
| **MCP Servers** | 6     | Model Context Protocol servers for AI tool integration |

**Coverage Areas:**

- Modern C# (patterns, nullable types, async/await)
- ASP.NET Core (Minimal APIs, Blazor, gRPC)
- Data Access (EF Core, Dapper)
- Testing (xUnit, integration testing)
- Cloud-Native (Docker, Kubernetes, Aspire)
- Mobile (MAUI)
- Security (OWASP)
- Performance (profiling, optimization)

## Repository Structure

```text
.rulesync/
├── skills/        # 193 knowledge modules
├── subagents/     # 18 specialized agents
├── commands/      # 28 slash commands
├── agents/        # 3 primary agents
└── mcp.json       # MCP server definitions
```

## MCP Inventory

MCP inventory (source: .rulesync/mcp.json): `context7`, `deepwiki`, `github`, `mcp-windbg`, `microsoftdocs-mcp`, `serena`

| MCP Server          | Description                                  |
| ------------------- | -------------------------------------------- |
| `context7`          | Context7 MCP server for documentation        |
| `deepwiki`          | DeepWiki MCP for repository documentation    |
| `github`            | GitHub MCP for repository operations         |
| `mcp-windbg`        | WinDbg MCP for debugging                     |
| `microsoftdocs-mcp` | Microsoft Learn documentation access         |
| `serena`            | Symbol-level code navigation and refactoring |

## License

MIT License. See [LICENSE](LICENSE).
