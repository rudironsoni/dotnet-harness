# Gemini CLI Verification Checklist

**Target ID:** `geminicli`  
**Runtime Shape:** Rules, ignore, MCP, commands, skills, hooks  
**Special Behavior:** Hooks must be thin and portable; no direct subagent consumption  
**Verification Date:** ___________  
**Verified By:** ___________

---

## Expected Files Generated

### Primary Configuration
- [ ] `.gemini/GEMINI.md` - Root rule file with overview content
- [ ] `.gemini/.geminiignore` - Ignore patterns for Gemini CLI

### Rules Directory
- [ ] `.gemini/rules/*.md` - Individual rule files from `.rulesync/rules/`
- [ ] `.gemini/rules/geminicli-overrides.md` - Target-specific rule overrides (if any)

### Commands Directory
- [ ] `.gemini/commands/*.md` - Command definitions from `.rulesync/commands/`
- [ ] Commands include frontmatter with `name`, `description`, `version`

### Skills Directory
- [ ] `.gemini/skills/**/*.md` - Skills from `.rulesync/skills/`
- [ ] Each skill has `SKILL.md` in its directory
- [ ] Skills include `targets: ['*']` or specific target list

### MCP Configuration
- [ ] `.gemini/mcp.json` - MCP server configuration
- [ ] Contains same servers as `.rulesync/mcp.json`

### Hooks (via RuleSync runtime)
- [ ] Hooks defined in `.rulesync/hooks.json` under `geminicli.hooks`
- [ ] Hooks are thin and portable (bash, jq, POSIX utilities)

---

## Feature Support Matrix

| Feature | Status | Notes |
|---------|--------|-------|
| Rules | Native | Full support via `.gemini/rules/` |
| Ignore Patterns | Native | Via `.gemini/.geminiignore` |
| Commands | Native | Full support with invocation |
| Subagents | N/A | Gemini does not consume subagents directly |
| Skills | Native | Full support |
| Hooks | Native | Must be thin and portable |
| MCP | Native | Full support with tool calling |
| Tool Support | Standard | Standard Gemini tool set |

**Key Differences from Claude Code:**
- **No subagent support** - Subagent guidance moved to rules/skills
- **Thin hooks** - Hooks must use portable shell utilities
- **Different file structure** - Follows Gemini CLI conventions

---

## Rules Verification

### Root Rule Location
- [ ] `.gemini/GEMINI.md` exists and is valid markdown
- [ ] Contains `root: true` in frontmatter
- [ ] Has `description` field in frontmatter
- [ ] Content includes overview of dotnet-agent-harness

### Non-Root Rules
- [ ] Rules from `.rulesync/rules/` copied to `.gemini/rules/`
- [ ] Each rule has valid frontmatter with `root: false`
- [ ] Rules have `globs` field specifying applicability
- [ ] Rules have `targets` including `'geminicli'`

### Gemini-Specific Adaptations
- [ ] Rules don't reference subagents (not supported)
- [ ] Subagent-like guidance moved to rules content
- [ ] Specialist topics covered directly in rules

---

## Commands Verification

### Location and Format
- [ ] Commands located in `.gemini/commands/`
- [ ] Each command file named `{command-name}.md`
- [ ] Frontmatter includes `name`, `description`, `targets`, `version`

### Command Content
- [ ] Commands include execution contract section
- [ ] Options documented with types and defaults
- [ ] Examples provided for common use cases

### Invocability Test
```bash
# In Gemini CLI, test command invocation:
gemini run "Execute dotnet-agent-harness:bootstrap"
gemini run "Run dotnet-agent-harness:search with query testing"
```
- [ ] Commands respond to invocation
- [ ] Help text displays correctly

---

## Subagents Verification

### Important: No Direct Subagent Support

Gemini CLI does NOT consume subagents directly. Instead:

1. **Subagent content** moved to rules or skills
2. **Specialist guidance** embedded in appropriate rules
3. **No `@mention`** or explicit subagent invocation

### Subagent Content Migration
Verify subagent content is available through:
- [ ] Rules covering specialist topics
- [ ] Skills with specialist knowledge
- [ ] Direct guidance in root rule

### Specialist Coverage
Ensure these areas are covered in rules/skills:
- [ ] Architecture guidance (was dotnet-architect)
- [ ] Testing guidance (was dotnet-testing-specialist)
- [ ] Security guidance (was dotnet-security-reviewer)
- [ ] Performance guidance (was dotnet-performance-analyst)

---

## Skills Verification

### Location and Format
- [ ] Skills in `.gemini/skills/{skill-name}/SKILL.md`
- [ ] Directory structure mirrors `.rulesync/skills/`
- [ ] Each skill has valid frontmatter

### Skill Content
- [ ] Skills include scope and out-of-scope sections
- [ ] Routing logic documented for complex skills
- [ ] Cross-references use `[skill:name]` syntax

### Loading Test
```bash
# In Gemini CLI:
gemini run "Load skill dotnet-advisor"
gemini run "Use skill dotnet-testing"
```
- [ ] Skills load without errors
- [ ] Skill content is accessible

---

## Hooks Verification

### Event Types Supported
| Event | Supported | Location in hooks.json |
|-------|-----------|------------------------|
| sessionStart | Yes | `hooks.sessionStart` (shared) |
| postToolUse | Yes | `geminicli.hooks.postToolUse` |
| beforeSubmitPrompt | No | Not supported |
| afterError | Yes | Shared or geminicli-specific |

### Critical: Thin and Portable Hooks

Gemini CLI hooks MUST be thin and use portable utilities:

**Allowed:**
- bash/sh scripts
- jq for JSON processing
- Standard POSIX utilities (grep, sed, awk, wc)
- Standard POSIX binaries (find, cat, test)

**Avoid:**
- Complex Python/Node scripts
- Platform-specific binaries
- Heavy dependencies

### Hook Configuration
- [ ] Hooks defined in `.rulesync/hooks.json`
- [ ] `geminicli.hooks` section uses portable commands
- [ ] Each hook has `type`, `command`, `timeout`
- [ ] Async hooks marked with `async: true`

### Hook Portability Test
```bash
# Verify hooks use portable utilities:
grep -E '(python|node|npm|pip)' .rulesync/hooks.json && echo "NON-PORTABLE" || echo "PORTABLE"
```
- [ ] No Python dependencies in hooks
- [ ] No Node.js dependencies in hooks
- [ ] Uses bash, jq, standard utilities only

---

## MCP Verification

### Location and Format
- [ ] `.gemini/mcp.json` exists
- [ ] Valid JSON syntax
- [ ] Same structure as `.rulesync/mcp.json`

### MCP Servers
| Server | Type | Status |
|--------|------|--------|
| serena | stdio | [ ] |
| microsoftdocs-mcp | http | [ ] |

### MCP Connection Test
```bash
# In Gemini CLI:
gemini run "Connect to serena MCP"
gemini run "Query microsoftdocs-mcp"
```
- [ ] MCP servers connect successfully
- [ ] Tools are listed and callable

---

## Ignore Verification

### Location
- [ ] `.gemini/.geminiignore` exists
- [ ] Content mirrors `.rulesync/.aiignore`

### Patterns Verified
- [ ] Build outputs ignored (bin/, obj/, *.dll)
- [ ] IDE files ignored (.vs/, .vscode/, *.user)
- [ ] Dependencies ignored (node_modules/, packages/)
- [ ] Test results ignored (TestResults/, coverage/)
- [ ] Secrets ignored (*.key, *.pem, .env)

---

## Common Issues and Fixes

### Issue: Subagent references failing
**Symptom:** Guidance references non-existent subagents

**Verification Steps:**
1. Check for `[subagent:name]` references in generated files
2. Verify subagent content migrated to rules
3. Check rules cover specialist topics

**Fix:** Replace subagent references:
```markdown
<!-- Before -->
Delegate to [subagent:dotnet-architect]

<!-- After -->
Consult the architecture guidance in this rule...
```

---

### Issue: Hooks using non-portable tools
**Symptom:** Hooks fail on systems without Python/Node

**Verification Steps:**
1. Check hook commands for python/node references
2. Verify only bash/jq/POSIX utilities used
3. Test hooks on minimal system

**Fix:** Rewrite hooks using portable utilities:
```bash
# Before (non-portable):
python3 -c "import json; ..."

# After (portable):
jq -r '.field' <<< "$JSON"
```

---

### Issue: Commands not responding
**Symptom:** Commands don't execute in Gemini CLI

**Verification Steps:**
1. Check command file exists in `.gemini/commands/`
2. Verify frontmatter has valid `name` field
3. Check `targets` includes `'geminicli'`

**Fix:** Regenerate with `rulesync generate --targets geminicli`

---

### Issue: MCP connection failures
**Symptom:** MCP servers show as unavailable

**Verification Steps:**
1. Check `.gemini/mcp.json` syntax
2. Verify required tools installed (uvx)
3. Check API keys configured

**Fix:** Validate JSON and install dependencies

---

## Post-Verification Summary

### Critical Checks Passed
- [ ] Root rule (GEMINI.md) valid and accessible
- [ ] No subagent references in generated content
- [ ] Core skills (dotnet-advisor) loadable
- [ ] Essential commands functional
- [ ] MCP servers connectable

### Gemini-Specific Verification
- [ ] All hooks use portable utilities only
- [ ] Subagent content migrated to rules/skills
- [ ] Specialist topics covered in rules

### Issues Found
| Issue | Severity | Status |
|-------|----------|--------|
| | | |

### Notes
___________
___________
