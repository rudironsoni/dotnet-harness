# GitHub Copilot CLI Verification Checklist

**Target ID:** `copilot`  
**Runtime Shape:** Rules, MCP, commands, subagents, skills  
**Special Behavior:** `agent/runSubagent` is implicit; tool-based execution  
**Verification Date:** ___________  
**Verified By:** ___________

---

## Expected Files Generated

### Primary Configuration
- [ ] `.github/copilot/AGENTS.md` - Root rule file with overview content
- [ ] May use inline prompts or separate instruction files

### Rules Directory
- [ ] `.github/copilot/rules/*.md` - Individual rule files from `.rulesync/rules/`
- [ ] Rules adapted for Copilot tool naming conventions

### Commands
- [ ] Commands embedded in rules or separate prompt files
- [ ] May use `copilot-instructions.md` format

### Subagents
- [ ] Subagents defined in agent files or inline
- [ ] Implicit `agent/runSubagent` capability

### Skills
- [ ] Skills referenced via inline prompts or catalog
- [ ] May be embedded in rules rather than separate files

### MCP Configuration
- [ ] MCP configuration in Copilot-compatible format
- [ ] May use `copilot-mcp.json` or inline configuration

---

## Feature Support Matrix

| Feature | Status | Notes |
|---------|--------|-------|
| Rules | Native | Via `.github/copilot/` or `copilot-instructions.md` |
| Ignore Patterns | N/A | Uses `.gitignore` or separate ignore config |
| Commands | Partial | Via inline prompts or command definitions |
| Subagents | Implicit | `agent/runSubagent` capability assumed |
| Skills | Partial | Referenced inline or via catalog |
| Hooks | N/A | Not natively supported |
| MCP | Native | Via MCP configuration |
| Tool Names | Copilot-specific | Uses Copilot tool naming conventions |

**Key Differences from Claude Code:**
- Tool names may differ (e.g., `read_file` vs `Read`)
- Subagent invocation is implicit, not explicit
- Hooks not supported natively
- May use different file structure

---

## Rules Verification

### Root Rule Location
- [ ] Root rule exists in Copilot-recognized location
- [ ] May be `copilot-instructions.md` or `.github/copilot/AGENTS.md`
- [ ] Contains overview of dotnet-agent-harness
- [ ] Includes targets specification

### Non-Root Rules
- [ ] Rules from `.rulesync/rules/` adapted for Copilot
- [ ] Tool names converted to Copilot conventions
- [ ] Rules have `targets` including `'copilot'`

### Copilot-Specific Adaptations
- [ ] Tool references use Copilot naming (`read_file`, `search_code`)
- [ ] Tool arrays formatted for Copilot parser
- [ ] Instructions are conversational and context-aware

---

## Commands Verification

### Location and Format
- [ ] Commands defined in rules or separate files
- [ ] May use natural language prompts rather than slash commands
- [ ] Frontmatter adapted for Copilot format

### Command Invocation
Copilot may not use explicit slash commands. Instead:
- [ ] Commands documented as natural language prompts
- [ ] Examples provided for "Ask Copilot to..."
- [ ] Command patterns recognized in conversation

### Example Test Prompts
```
# Test command-like functionality:
"Bootstrap dotnet-agent-harness with targets claudecode and opencode"
"Search for testing skills in the dotnet-agent-harness catalog"
"Recommend skills for this .NET project"
```

---

## Subagents Verification

### Important: Implicit Subagent Support

Copilot CLI has implicit `agent/runSubagent` capability. This means:

1. **No explicit subagent files** required in many cases
2. **Subagents invoked** via natural language
3. **Context switching** handled by Copilot runtime

### Subagent Patterns
- [ ] Subagent capabilities documented in rules
- [ ] Trigger lexicon included for agent routing
- [ ] Example prompts show subagent invocation

### Subagent Test Prompts
```
# Test subagent invocation:
"Ask the dotnet-architect to review this solution structure"
"Have the dotnet-testing-specialist help me write xUnit tests"
"Get the dotnet-security-reviewer to audit this code"
```

---

## Skills Verification

### Skill References
- [ ] Skills referenced in rules with `[skill:name]` syntax
- [ ] Skill catalog documented for Copilot context
- [ ] Major skill categories listed (testing, architecture, security, etc.)

### Skill Loading
Copilot may not have explicit skill loading. Instead:
- [ ] Skills embedded in context via rules
- [ ] Skill content available through conversation
- [ ] Cross-references resolved in generated prompts

---

## MCP Verification

### Location and Format
- [ ] MCP configuration in Copilot-compatible location
- [ ] May use `copilot-mcp.json` or similar
- [ ] Valid JSON syntax

### MCP Servers
| Server | Type | Status |
|--------|------|--------|
| serena | stdio | [ ] |
| microsoftdocs-mcp | http | [ ] |

### MCP Connection Test
- [ ] MCP servers configured correctly
- [ ] http servers accessible
- [ ] stdio servers have correct command paths

---

## Ignore Verification

### Copilot Ignore Behavior
- [ ] Uses `.gitignore` for basic exclusions
- [ ] May have separate Copilot ignore configuration
- [ ] Build outputs, secrets excluded from context

### Verification
- [ ] Sensitive files not included in Copilot context
- [ ] Binary files excluded
- [ ] Generated files excluded appropriately

---

## Tool Name Mapping

Copilot uses different tool names than Claude Code. Verify mappings:

| Claude Code | Copilot | Purpose |
|-------------|---------|---------|
| Read | read_file | Read file contents |
| Edit | apply_patch | Edit file contents |
| Write | write_file | Create new files |
| Bash | run_command | Execute shell commands |
| Grep | search_code | Search code patterns |
| Glob | list_files | List files matching pattern |

### Verification
- [ ] Tool references use Copilot naming in generated rules
- [ ] Tool permissions correctly specified
- [ ] Complex operations use appropriate tool sequences

---

## Common Issues and Fixes

### Issue: Commands not recognized
**Symptom:** Copilot doesn't understand command requests

**Verification Steps:**
1. Check commands documented in natural language
2. Verify command patterns are clear and specific
3. Ensure examples provided

**Fix:** Add explicit examples:
```markdown
## Commands

To bootstrap the harness, ask: "Bootstrap dotnet-agent-harness with targets claudecode and opencode"
```

---

### Issue: Subagents not routing correctly
**Symptom:** Copilot doesn't switch to specialist context

**Verification Steps:**
1. Check trigger lexicon is comprehensive
2. Verify subagent capabilities documented
3. Ensure example prompts include agent names

**Fix:** Add explicit routing instructions:
```markdown
When the user asks about testing, invoke the dotnet-testing-specialist context.
```

---

### Issue: Tool names not recognized
**Symptom:** Copilot reports unknown tools

**Verification Steps:**
1. Check tool names use Copilot conventions
2. Verify tool references are valid
3. Check for typos in tool names

**Fix:** Update tool references to Copilot naming:
```markdown
<!-- Before -->
Use Read to examine the file.

<!-- After -->
Use read_file to examine the file.
```

---

### Issue: MCP servers not connecting
**Symptom:** MCP tools unavailable in Copilot

**Verification Steps:**
1. Check MCP configuration format
2. Verify Copilot supports MCP servers
3. Check API keys and authentication

**Fix:** Ensure MCP config follows Copilot's expected format

---

## Post-Verification Summary

### Critical Checks Passed
- [ ] Root rule accessible in Copilot context
- [ ] Core dotnet guidance available
- [ ] Tool names use Copilot conventions
- [ ] MCP servers configured

### Copilot-Specific Verification
- [ ] Natural language prompts work for commands
- [ ] Subagent routing via context switching
- [ ] Tool permissions appropriate

### Issues Found
| Issue | Severity | Status |
|-------|----------|--------|
| | | |

### Notes
___________
___________
