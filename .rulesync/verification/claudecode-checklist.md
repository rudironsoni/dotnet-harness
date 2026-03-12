# Claude Code Verification Checklist

**Target ID:** `claudecode`  
**Runtime Shape:** Rules, ignore, MCP, commands, subagents, skills, hooks  
**Verification Date:** ___________  
**Verified By:** ___________

---

## Expected Files Generated

### Primary Configuration
- [ ] `.claude/CLAUDE.md` - Root rule file with overview content
- [ ] `.claude/.claudeignore` - Ignore patterns for Claude Code

### Rules Directory
- [ ] `.claude/rules/*.md` - Individual rule files from `.rulesync/rules/`
- [ ] `.claude/rules/claudecode-overrides.md` - Target-specific rule overrides (if any)

### Commands Directory
- [ ] `.claude/commands/*.md` - Command definitions from `.rulesync/commands/`
- [ ] Commands include frontmatter with `name`, `description`, `version`

### Subagents Directory
- [ ] `.claude/agents/*.md` - Subagent definitions from `.rulesync/subagents/`
- [ ] Agents include `allowed-tools` in frontmatter
- [ ] Agents include trigger lexicon and example prompts

### Skills Directory
- [ ] `.claude/skills/**/*.md` - Skills from `.rulesync/skills/`
- [ ] Each skill has `SKILL.md` in its directory
- [ ] Skills include `targets: ['*']` or specific target list

### MCP Configuration
- [ ] `.claude/mcp.json` - MCP server configuration
- [ ] Contains same servers as `.rulesync/mcp.json`

### Hooks (via RuleSync runtime)
- [ ] Hooks defined in `.rulesync/hooks.json` under `claudecode.hooks`

---

## Feature Support Matrix

| Feature | Status | Notes |
|---------|--------|-------|
| Rules | Native | Full support via `.claude/rules/` |
| Ignore Patterns | Native | Via `.claude/.claudeignore` |
| Commands | Native | Full support with slash invocation |
| Subagents | Native | Full support via `@agent` mention |
| Skills | Native | Full support with `[skill:name]` syntax |
| Hooks | Native | Via RuleSync hook runtime |
| MCP | Native | Full support with tool calling |
| Tool Filtering | Native | `allowed-tools` in frontmatter |

---

## Rules Verification

### Root Rule Location
- [ ] `.claude/CLAUDE.md` exists and is valid markdown
- [ ] Contains `root: true` in frontmatter
- [ ] Has `description` field in frontmatter
- [ ] Content includes overview of dotnet-agent-harness

### Non-Root Rules
- [ ] Rules from `.rulesync/rules/` copied to `.claude/rules/`
- [ ] Each rule has valid frontmatter with `root: false`
- [ ] Rules have `globs` field specifying applicability
- [ ] Rules have `targets` including `'claudecode'`

### Rule Content Verification
- [ ] No duplicate root rules exist
- [ ] All rule files are valid markdown
- [ ] Cross-references between rules use relative paths
- [ ] Rule content is substantive (not placeholder)

---

## Commands Verification

### Location and Format
- [ ] Commands located in `.claude/commands/`
- [ ] Each command file named `{command-name}.md`
- [ ] Frontmatter includes `name`, `description`, `targets`, `version`

### Command Content
- [ ] Commands include execution contract section
- [ ] Options documented with types and defaults
- [ ] Examples provided for common use cases
- [ ] Notes section explains edge cases

### Invocability Test
```bash
# In Claude Code, test command invocation:
/dotnet-agent-harness:bootstrap --help
/dotnet-agent-harness:search --query "testing"
```
- [ ] Commands respond to `/command:name` syntax
- [ ] Help text displays correctly
- [ ] Options are parsed correctly

---

## Subagents Verification

### Location and Format
- [ ] Subagents located in `.claude/agents/`
- [ ] Each agent file named `{agent-name}.md`
- [ ] Frontmatter includes `name`, `description`, `targets`

### Agent Configuration
- [ ] `allowed-tools` specified in frontmatter
- [ ] Tools listed are Claude Code compatible (Read, Edit, Write, Bash, Grep, Glob)
- [ ] `claudecode` block includes `model: inherit` or specific model

### Agent Content
- [ ] Trigger lexicon documented
- [ ] Example prompts provided
- [ ] Workflow section explains operation steps
- [ ] Knowledge sources cited with disclaimers

### Invocation Test
- [ ] `@dotnet-architect` mention triggers agent
- [ ] Agent loads specified skills
- [ ] Agent respects tool restrictions

---

## Skills Verification

### Location and Format
- [ ] Skills in `.claude/skills/{skill-name}/SKILL.md`
- [ ] Directory structure mirrors `.rulesync/skills/`
- [ ] Each skill has valid frontmatter

### Skill Content
- [ ] Skills include scope and out-of-scope sections
- [ ] Routing logic documented for complex skills
- [ ] Cross-references use `[skill:name]` syntax
- [ ] Code examples provided where applicable

### Loading Test
```
# In Claude Code:
Load skill:dotnet-advisor
Load skill:dotnet-testing
```
- [ ] Skills load without errors
- [ ] Skill content is accessible
- [ ] Cross-references resolve correctly

---

## Hooks Verification

### Event Types Supported
| Event | Supported | Location in hooks.json |
|-------|-----------|------------------------|
| sessionStart | Yes | `claudecode.hooks.sessionStart` |
| postToolUse | Yes | `claudecode.hooks.postToolUse` |
| beforeSubmitPrompt | Yes | `claudecode.hooks.beforeSubmitPrompt` |
| afterError | Yes | `claudecode.hooks.afterError` |

### Hook Configuration
- [ ] Hooks defined in `.rulesync/hooks.json`
- [ ] `claudecode.hooks` section exists
- [ ] Each hook has `type`, `command`, `timeout`
- [ ] Async hooks marked with `async: true`
- [ ] Matchers use correct tool names (Write, Edit, Bash)

### Hook Execution Test
- [ ] sessionStart hooks fire on workspace load
- [ ] postToolUse hooks fire after Write/Edit operations
- [ ] Matcher patterns work correctly
- [ ] Timeout values are reasonable (10-60s)

---

## MCP Verification

### Location and Format
- [ ] `.claude/mcp.json` exists
- [ ] Valid JSON syntax
- [ ] Same structure as `.rulesync/mcp.json`

### MCP Servers
| Server | Type | Status |
|--------|------|--------|
| serena | stdio | [ ] |
| microsoftdocs-mcp | http | [ ] |

### MCP Connection Test
```
# In Claude Code, test MCP connectivity:
Use mcp:serena
Use mcp:microsoftdocs-mcp
```
- [ ] MCP servers connect successfully
- [ ] Tools are listed and callable
- [ ] stdio servers have correct command paths
- [ ] http servers have valid URLs

---

## Ignore Verification

### Location
- [ ] `.claude/.claudeignore` exists
- [ ] Content mirrors `.rulesync/.aiignore`

### Patterns Verified
- [ ] Build outputs ignored (bin/, obj/, *.dll)
- [ ] IDE files ignored (.vs/, .vscode/, *.user)
- [ ] Dependencies ignored (node_modules/, packages/)
- [ ] Test results ignored (TestResults/, coverage/)
- [ ] Secrets ignored (*.key, *.pem, .env)

### Effectiveness Test
- [ ] Ignored files don't appear in Claude Code file list
- [ ] Globs work correctly for nested directories

---

## Common Issues and Fixes

### Issue: Commands not appearing
**Symptom:** `/command:name` returns "command not found"

**Verification Steps:**
1. Check command file exists in `.claude/commands/`
2. Verify frontmatter has valid `name` field
3. Ensure `targets` includes `'claudecode'`

**Fix:** Regenerate with `rulesync generate --targets claudecode`

---

### Issue: Subagents not loading
**Symptom:** `@agent` mention doesn't trigger agent

**Verification Steps:**
1. Check agent file exists in `.claude/agents/`
2. Verify `name` in frontmatter matches mention name
3. Check for syntax errors in frontmatter

**Fix:** Validate markdown frontmatter with YAML linter

---

### Issue: Hooks not firing
**Symptom:** post-edit actions don't execute

**Verification Steps:**
1. Check `claudecode.hooks` section in `.rulesync/hooks.json`
2. Verify command paths are correct
3. Check executable permissions on shell scripts
4. Review hook timeout values

**Fix:**
```bash
chmod +x .rulesync/hooks/*.sh
# Verify hook JSON syntax
jq '.' .rulesync/hooks.json > /dev/null && echo "Valid JSON"
```

---

### Issue: MCP connection failures
**Symptom:** MCP tools show as unavailable

**Verification Steps:**
1. Check `.claude/mcp.json` syntax
2. Verify required tools installed (uvx, node)
3. Test server connectivity manually

**Fix:**
```bash
# Test stdio server
uvx --from git+https://github.com/oraios/serena serena --help

# Test http server
curl -I https://learn.microsoft.com/api/mcp
```

---

### Issue: Rules not applying
**Symptom:** Rule guidance doesn't appear in context

**Verification Steps:**
1. Check rule file exists in `.claude/rules/`
2. Verify `globs` pattern matches target files
3. Check `targets` includes `'claudecode'`
4. Ensure no duplicate root rules

**Fix:** Review rule frontmatter and regenerate

---

## Post-Verification Summary

### Critical Checks Passed
- [ ] Root rule (CLAUDE.md) valid and accessible
- [ ] Core skills (dotnet-advisor, dotnet-csharp-coding-standards) loadable
- [ ] Essential commands (/dotnet-agent-harness:bootstrap) functional
- [ ] MCP servers (serena, microsoftdocs-mcp) connectable

### Optional Features Verified
- [ ] All 18 subagents loadable
- [ ] All hooks fire correctly
- [ ] All ignore patterns effective

### Issues Found
| Issue | Severity | Status |
|-------|----------|--------|
| | | |

### Notes
___________
___________
