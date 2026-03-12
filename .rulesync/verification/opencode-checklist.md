# OpenCode Verification Checklist

**Target ID:** `opencode`  
**Runtime Shape:** Rules, MCP, commands, subagents, skills, hooks  
**Special Behavior:** Tab cycles primary agents only; `@mention` invokes subagents  
**Verification Date:** ___________  
**Verified By:** ___________

---

## Expected Files Generated

### Primary Configuration
- [ ] `.opencode/AGENTS.md` - Root rule file with overview content
- [ ] `.opencode/.agentignore` - Ignore patterns for OpenCode

### Rules Directory
- [ ] `.opencode/rules/*.md` - Individual rule files from `.rulesync/rules/`
- [ ] `.opencode/rules/opencode-overrides.md` - Target-specific rule overrides (if any)

### Commands Directory
- [ ] `.opencode/commands/*.md` - Command definitions from `.rulesync/commands/`
- [ ] Commands include frontmatter with `name`, `description`, `version`

### Subagents Directory
- [ ] `.opencode/agents/*.md` - Subagent definitions from `.rulesync/subagents/`
- [ ] Agents include `mode: primary` or `mode: subagent` in frontmatter
- [ ] Primary agents appear in Tab rotation

### Skills Directory
- [ ] `.opencode/skills/**/*.md` - Skills from `.rulesync/skills/`
- [ ] Each skill has `SKILL.md` in its directory
- [ ] Skills include `targets: ['*']` or specific target list

### MCP Configuration
- [ ] `.opencode/mcp.json` - MCP server configuration
- [ ] Contains same servers as `.rulesync/mcp.json`

### Hooks (via RuleSync runtime)
- [ ] Hooks defined in `.rulesync/hooks.json` under `opencode.hooks`

---

## Feature Support Matrix

| Feature | Status | Notes |
|---------|--------|-------|
| Rules | Native | Full support via `.opencode/rules/` |
| Ignore Patterns | Native | Via `.opencode/.agentignore` |
| Commands | Native | Full support with command invocation |
| Subagents | Partial | Via `@mention`, not direct import |
| Skills | Native | Full support |
| Hooks | Native | Via RuleSync hook runtime |
| MCP | Native | Full support with tool calling |
| Tab Rotation | Native | Only `mode: primary` agents |

**Key Difference from Claude Code:**
- Subagents are NOT imported as separate agent files
- Subagents are invoked via `@mention` syntax
- Only agents marked `mode: primary` appear in Tab completion

---

## Rules Verification

### Root Rule Location
- [ ] `.opencode/AGENTS.md` exists and is valid markdown
- [ ] Contains `root: true` in frontmatter
- [ ] Has `description` field in frontmatter
- [ ] Content includes overview of dotnet-agent-harness

### Non-Root Rules
- [ ] Rules from `.rulesync/rules/` copied to `.opencode/rules/`
- [ ] Each rule has valid frontmatter with `root: false`
- [ ] Rules have `globs` field specifying applicability
- [ ] Rules have `targets` including `'opencode'`

### OpenCode-Specific Rule Blocks
- [ ] Rules with `opencode:` blocks have target-specific configuration
- [ ] `opencode.trigger` specified where needed (`always_on`, etc.)

---

## Commands Verification

### Location and Format
- [ ] Commands located in `.opencode/commands/`
- [ ] Each command file named `{command-name}.md`
- [ ] Frontmatter includes `name`, `description`, `targets`, `version`

### Command Content
- [ ] Commands include execution contract section
- [ ] Options documented with types and defaults
- [ ] Examples provided for common use cases

### Invocability Test
```
# In OpenCode, test command invocation:
dotnet-agent-harness:bootstrap --help
dotnet-agent-harness:search --query "testing"
```
- [ ] Commands respond to command invocation
- [ ] Help text displays correctly

---

## Subagents Verification

### Important: OpenCode Subagent Model

OpenCode does NOT directly import subagent files like Claude Code. Instead:

1. **Tab rotation** only includes `mode: primary` agents
2. **Subagents** are invoked via `@mention` syntax
3. **Agent configuration** comes from frontmatter in agent files

### Agent File Requirements
- [ ] Agent files exist in `.opencode/agents/` (for reference)
- [ ] Primary agents have `opencode.mode: primary`
- [ ] Subagents have `opencode.mode: subagent` (or omitted)
- [ ] `hidden: false` for agents that should be discoverable

### Primary Agent Verification
**Primary agents appearing in Tab rotation:**
- [ ] `dotnet-architect` marked as primary
- [ ] Primary agent frontmatter includes `tools` specification
- [ ] `bash`, `edit`, `write` permissions correctly set

### Subagent Invocation Test
```
# In OpenCode, test subagent invocation:
@dotnet-testing-specialist help me write unit tests
@dotnet-architect review this solution structure
```
- [ ] `@mention` triggers subagent context
- [ ] Subagent loads specified skills
- [ ] Subagent operates within tool permissions

---

## Skills Verification

### Location and Format
- [ ] Skills in `.opencode/skills/{skill-name}/SKILL.md`
- [ ] Directory structure mirrors `.rulesync/skills/`
- [ ] Each skill has valid frontmatter

### Skill Content
- [ ] Skills include scope and out-of-scope sections
- [ ] Routing logic documented for complex skills
- [ ] Cross-references use `[skill:name]` syntax

### Loading Test
```
# In OpenCode:
Load skill dotnet-advisor
Load skill dotnet-testing
```
- [ ] Skills load without errors
- [ ] Skill content is accessible
- [ ] Cross-references resolve correctly

---

## Hooks Verification

### Event Types Supported
| Event | Supported | Location in hooks.json |
|-------|-----------|------------------------|
| sessionStart | Yes | `hooks.sessionStart` (shared) |
| postToolUse | Yes | `opencode.hooks.postToolUse` |
| beforeSubmitPrompt | No | Not supported |
| afterError | Yes | `opencode.hooks` via matcher |

### Hook Configuration
- [ ] Hooks defined in `.rulesync/hooks.json`
- [ ] `opencode.hooks` section exists for target-specific hooks
- [ ] Shared hooks in root `hooks` section apply
- [ ] Each hook has `type`, `command`, `timeout`

### Hook Execution Test
- [ ] sessionStart hooks fire on workspace load
- [ ] postToolUse hooks fire after write/edit operations
- [ ] Matcher patterns work correctly

---

## MCP Verification

### Location and Format
- [ ] `.opencode/mcp.json` exists
- [ ] Valid JSON syntax
- [ ] Same structure as `.rulesync/mcp.json`

### MCP Servers
| Server | Type | Status |
|--------|------|--------|
| serena | stdio | [ ] |
| microsoftdocs-mcp | http | [ ] |

### MCP Connection Test
- [ ] MCP servers connect successfully
- [ ] Tools are listed and callable
- [ ] stdio servers have correct command paths

---

## Ignore Verification

### Location
- [ ] `.opencode/.agentignore` exists
- [ ] Content mirrors `.rulesync/.aiignore`

### Patterns Verified
- [ ] Build outputs ignored (bin/, obj/, *.dll)
- [ ] IDE files ignored (.vs/, .vscode/, *.user)
- [ ] Dependencies ignored (node_modules/, packages/)
- [ ] Test results ignored (TestResults/, coverage/)
- [ ] Secrets ignored (*.key, *.pem, .env)

---

## Common Issues and Fixes

### Issue: Subagent not appearing in Tab
**Symptom:** Agent doesn't show in Tab rotation

**Verification Steps:**
1. Check agent file has `opencode.mode: primary`
2. Verify `hidden: false` in frontmatter
3. Ensure `targets` includes `'opencode'`

**Fix:** Add or correct the OpenCode frontmatter block:
```yaml
opencode:
  mode: primary
  hidden: false
  tools:
    bash: true
    edit: false
    write: false
```

---

### Issue: @mention not working
**Symptom:** `@agentname` doesn't invoke subagent

**Verification Steps:**
1. Check agent name spelling matches frontmatter
2. Verify agent file exists (even if not primary)
3. Check for syntax errors in frontmatter

**Fix:** Ensure agent name in frontmatter matches expected mention name

---

### Issue: Command permissions too broad
**Symptom:** Commands can modify files unexpectedly

**Verification Steps:**
1. Review command frontmatter tool permissions
2. Check `edit` and `write` flags

**Fix:** Set restrictive permissions for read-only commands:
```yaml
opencode:
  tools:
    bash: true
    edit: false
    write: false
```

---

### Issue: MCP tools not appearing
**Symptom:** MCP servers show as disconnected

**Verification Steps:**
1. Check `.opencode/mcp.json` syntax
2. Verify network connectivity for http servers
3. Check required binaries for stdio servers

**Fix:** Validate MCP JSON and install required dependencies

---

## Post-Verification Summary

### Critical Checks Passed
- [ ] Root rule (AGENTS.md) valid and accessible
- [ ] At least one primary agent configured
- [ ] Core skills (dotnet-advisor) loadable
- [ ] Essential commands functional
- [ ] MCP servers connectable

### OpenCode-Specific Verification
- [ ] Tab rotation shows primary agents only
- [ ] `@mention` syntax works for subagents
- [ ] Agent permissions appropriate per agent type

### Issues Found
| Issue | Severity | Status |
|-------|----------|--------|
| | | |

### Notes
___________
___________
