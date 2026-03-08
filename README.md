# dotnet-agent-harness

> **The definitive .NET development companion for AI coding tools.**
>
> 193 specialized skills · 18 expert subagents · 28 powerful commands

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

---

## Use in a Repo

Install the toolkit in your .NET repository:

```bash
# Install the tool
dotnet new tool-manifest
dotnet tool install Rudironsoni.DotNetAgentHarness

# Bootstrap your repo with agents
dotnet agent-harness bootstrap
```

That's it! The bootstrap command will install the appropriate agent configurations for your project.

## Available Commands

After installation, use these commands:

- `dotnet agent-harness bootstrap` - Install/update agent configurations
- `dotnet agent-harness doctor` - Check installation health
- `dotnet agent-harness recommend` - Get skill recommendations for your project
- `dotnet agent-harness search <query>` - Search the skill catalog
- `dotnet agent-harness prepare` - Assemble prompt bundles
- `dotnet agent-harness validate` - Run validation checks

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
