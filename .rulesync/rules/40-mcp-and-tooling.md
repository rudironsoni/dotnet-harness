---
title: MCP Server Policies and Tooling Strategy
nav_order: 40
targets: ['*']
version: '0.0.1'
---

# MCP Server Policies and Tooling Strategy

This document defines the MCP server inventory, intended use cases, routing preferences, and health check expectations for the dotnet-agent-harness ecosystem.

## MCP Server Inventory

The following MCP servers are configured in `.rulesync/mcp.json`:

| Server | Type | Primary Role | Best Use Cases |
|--------|------|--------------|----------------|
| **serena** | stdio | Semantic code analysis | Symbol-level navigation, refactoring, dependency analysis, precise code edits |
| **microsoftdocs-mcp** | http | Official Microsoft documentation | First-party .NET/ASP.NET/Azure docs, authoritative API references |

## MCP Routing Guide

### Primary Routing Decision Tree

```text
Need to navigate or refactor code?
  YES -> [mcp:serena] for symbol operations
  NO -> Continue...

Need official Microsoft documentation?
  YES -> [mcp:microsoftdocs-mcp] for .NET/Azure docs
  NO -> Continue...

Need documentation?
  Official Microsoft docs -> [mcp:microsoftdocs-mcp] for .NET/Azure docs
  Third-party libraries -> Use web search with library READMEs
  Project-specific docs -> Read markdown files from docs/, wiki/, or repo
  GitHub operations -> Use gh CLI or GitHub web interface
  Other -> Traditional tools (Read, Grep, Glob, Bash)
```

### Server-Specific Guidance

#### Serena (Code Navigation & Refactoring)

**Preferred for:**
- Finding symbol definitions across the codebase
- Understanding file structure and organization
- Tracking symbol references for impact analysis
- Precise code edits with automatic reference updates
- Renaming symbols with full codebase propagation

**Tool mapping:**
- `serena_find_symbol` → Find class/method definitions
- `serena_get_symbols_overview` → Understand file structure
- `serena_find_referencing_symbols` → Track dependencies
- `serena_replace_symbol_body` → Precise modifications
- `serena_rename_symbol` → Safe refactoring

**When to prefer over traditional tools:**
- ✅ Navigation tasks (vs Grep for finding symbols)
- ✅ Refactoring operations (vs Edit for symbol bodies)
- ✅ Dependency analysis (vs manual code inspection)
- ✅ Multi-file changes requiring reference updates

**Fallback:** If Serena unavailable, use Read + Grep + Edit.

#### MicrosoftDocs MCP

**Preferred for:**
- Official .NET API documentation
- ASP.NET Core guidance
- Azure SDK documentation
- Breaking changes and migration guides
- Security advisories and best practices

**Tool mapping:**
- `microsoftdocs-mcp_microsoft_docs_search` → Search official docs
- `microsoftdocs-mcp_microsoft_code_sample_search` → Find code samples
- `microsoftdocs-mcp_microsoft_docs_fetch` → Read full articles

**When to use:**
- ✅ Before implementing features using Microsoft APIs
- ✅ When unsure about API availability in specific TFMs
- ✅ For authoritative answers on .NET behavior
- ✅ Security and compliance guidance

**Fallback:** Use web search with official microsoft.com/learn sources.

#### Documentation Strategy

**For Official Microsoft Documentation:**
- Use [mcp:microsoftdocs-mcp] for .NET, ASP.NET Core, and Azure SDK docs
- Fallback: Web search with site:microsoft.com/learn

**For Third-Party Libraries:**
- Use web search targeting GitHub repos, official docs, or NuGet.org
- Read README.md and API documentation directly

**For Project Documentation:**
- Read markdown files from docs/, wiki/, or repository root
- Use Grep to search for patterns across documentation

**For GitHub Operations:**
- Use gh CLI (GitHub CLI) for repository operations
- Use web interface for complex PR reviews or issue management

## Target-Specific Tool Filtering

### OpenCode Behavior

- Tab cycles **primary** agents only
- `@mention` invokes subagents
- Tool availability varies by agent configuration
- Some MCP servers may be filtered based on target surface

### Codex CLI Behavior

- Full MCP server access
- All configured servers available
- No tool filtering applied

### Copilot Behavior

- Uses `execute` tool for shell commands
- MCP access via extension points
- May require explicit `@github` mentions

### Claude Code Behavior

- Native MCP server support
- All configured servers available via tool calls
- Automatic routing based on tool names

### Gemini CLI Behavior

- MCP support via configuration
- Similar to Claude Code for stdio/http servers

## Health Check Expectations

### Session Start Validation

Every session should validate MCP server health:

1. **Check configuration:** Ensure `.rulesync/mcp.json` exists and is valid
2. **Test connectivity:** 
   - HTTP servers: Verify URL accessibility
   - STDIO servers: Verify command availability (uvx, npx, etc.)
3. **Report status:** Output which servers are available/unavailable
4. **Suggest alternatives:** Provide fallback guidance for unavailable servers

### Health Check Output Format

```json
{
    "mcpHealth": {
    "serena": { "status": "available", "type": "stdio" },
    "microsoftdocs-mcp": { "status": "available", "type": "http" }
  },
  "recommendations": []
}
```

### Failure Handling

| Server Down | Impact | Mitigation |
|-------------|--------|------------|
| serena | Cannot perform symbol navigation | Use Read + Grep for finding code |
| microsoftdocs-mcp | No official doc queries | Use web search with microsoft.com/learn |

## MCP Usage Patterns by Agent

### dotnet-architect

**Preferred MCPs:**
1. **serena** - Analyze existing solution structure
2. **microsoftdocs-mcp** - Validate framework choices against official guidance

**Example workflow:**
```
1. [mcp:serena] Analyze solution structure
2. [mcp:microsoftdocs-mcp] Query official architecture guidance
3. Read project docs/ or wiki/ for conventions
4. Recommend approach based on findings
```

### dotnet-code-review-agent

**Preferred MCPs:**
1. **serena** - Navigate code under review
2. **microsoftdocs-mcp** - Verify API usage against docs

**Example workflow:**
```
1. [mcp:serena] Find symbols in changed files
2. Review code with symbol context
3. [mcp:microsoftdocs-mcp] Validate API usage
4. Web search for third-party library patterns if needed
```

### dotnet-advisor (Router)

**Preferred MCPs:**
1. **serena** - Quick project analysis
2. **microsoftdocs-mcp** - Skill documentation validation

**Example workflow:**
```
1. [mcp:serena] Get project overview
2. Route to appropriate skill
3. [mcp:microsoftdocs-mcp] Validate skill guidance
```

## Implementation Notes

### Adding New MCP Servers

When adding new MCP servers:

1. Add entry to `.rulesync/mcp.json`
2. Document role and use cases in this file
3. Update routing guidance in relevant subagents
4. Add health check logic to session-start hook
5. Update target-specific configurations if needed

### MCP Server Categories

| Category | Servers | Use When |
|----------|---------|----------|
| **Code Intelligence** | serena | Navigation, refactoring, analysis |
| **Documentation** | microsoftdocs-mcp | Official Microsoft docs, research |
| **Operations** | (Use gh CLI) | GitHub repository management |

### Performance Considerations

- **HTTP MCPs:** May have network latency; cache results when appropriate
- **STDIO MCPs:** Startup cost on first use; keep sessions alive
- **Parallel calls:** Independent MCP calls can be parallelized
- **Fallbacks:** Always have traditional tool fallback for offline scenarios

## References

- MCP Configuration: `.rulesync/mcp.json`
- Health Check Hook: `.rulesync/hooks/session-start.sh`
- Serena Documentation: https://github.com/oraios/serena
- MCP Specification: https://modelcontextprotocol.io/
