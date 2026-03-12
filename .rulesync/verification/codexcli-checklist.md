# Codex CLI Verification Checklist

**Target ID:** `codexcli`  
**Runtime Shape:** Rules, ignore, MCP, subagents, skills  
**Special Behavior:** Read-only sandbox mode; commands and hooks not native surfaces  
**Verification Date:** ___________  
**Verified By:** ___________

---

## Expected Files Generated

### Primary Configuration
- [ ] `.codex/CODEX.md` - Root rule file with overview content
- [ ] `.codex/.codexignore` - Ignore patterns for Codex CLI

### Rules Directory
- [ ] `.codex/rules/*.md` - Individual rule files from `.rulesync/rules/`
- [ ] `.codex/rules/codexcli-overrides.md` - Target-specific rule overrides (if any)

### Subagents Directory
- [ ] `.codex/agents/*.md` - Subagent definitions from `.rulesync/subagents/`
- [ ] Agents include `sandbox_mode` in frontmatter
- [ ] Default sandbox mode is `read-only`

### Skills Directory
- [ ] `.codex/skills/**/*.md` - Skills from `.rulesync/skills/`
- [ ] Each skill has `SKILL.md` in its directory
- [ ] Skills include `targets: ['*']` or specific target list

### MCP Configuration
- [ ] `.codex/mcp.json` - MCP server configuration
- [ ] Contains same servers as `.rulesync/mcp.json`

### Commands and Hooks
- [ ] Commands NOT generated (not native surface)
- [ ] Hooks NOT generated (not native surface)
- [ ] Important guidance moved to rules instead

---

## Feature Support Matrix

| Feature | Status | Notes |
|---------|--------|-------|
| Rules | Native | Full support via `.codex/rules/` |
| Ignore Patterns | Native | Via `.codex/.codexignore` |
| Commands | N/A | Not a native target surface |
| Subagents | Native | With sandbox_mode restrictions |
| Skills | Native | Full support |
| Hooks | N/A | Not a native target surface |
| MCP | Native | Full support with tool calling |
| Sandbox Mode | Required | `sandbox_mode: "read-only"` default |

**Key Differences from Claude Code:**
- **Read-only by default** - Express with `sandbox_mode: "read-only"`
- **No commands** - Commands not a native Codex surface
- **No hooks** - Hooks not a native Codex surface
- **Sandbox restrictions** - Subagents have limited tool access

---

## Rules Verification

### Root Rule Location
- [ ] `.codex/CODEX.md` exists and is valid markdown
- [ ] Contains `root: true` in frontmatter
- [ ] Has `description` field in frontmatter
- [ ] Content includes overview of dotnet-agent-harness

### Non-Root Rules
- [ ] Rules from `.rulesync/rules/` copied to `.codex/rules/`
- [ ] Each rule has valid frontmatter with `root: false`
- [ ] Rules have `globs` field specifying applicability
- [ ] Rules have `targets` including `'codexcli'`

### Codex-Specific Adaptations
- [ ] No command references (not supported)
- [ ] No hook references (not supported)
- [ ] Command-like guidance moved to rules content
- [ ] Hook-like guidance moved to rules content

### Command Migration
Verify these command concepts are available as rules:
- [ ] Bootstrap guidance in rules
- [ ] Search functionality documented
- [ ] Recommendations available via skills

---

## Subagents Verification

### Important: Sandbox Mode Required

Codex CLI subagents MUST specify sandbox mode:

```yaml
codexcli:
  sandbox_mode: "read-only"  # Default, recommended
  # OR
  sandbox_mode: inherit  # Inherit from parent
```

### Location and Format
- [ ] Subagents located in `.codex/agents/`
- [ ] Each agent file named `{agent-name}.md`
- [ ] Frontmatter includes `name`, `description`, `targets`

### Agent Configuration
- [ ] `codexcli.sandbox_mode` specified
- [ ] Default is `"read-only"`
- [ ] Tools restricted appropriately
- [ ] `short-description` for Codex CLI context

### Agent Content
- [ ] Trigger lexicon documented
- [ ] Example prompts provided
- [ ] Workflow section explains read-only analysis
- [ ] No file modification instructions

### Invocation Test
```bash
# In Codex CLI, test subagent invocation:
codex "Ask dotnet-architect to analyze this solution structure"
codex "Have dotnet-code-review-agent review this PR"
```
- [ ] Subagents invoked via natural language
- [ ] Subagents respect sandbox restrictions
- [ ] No write operations attempted

---

## Skills Verification

### Location and Format
- [ ] Skills in `.codex/skills/{skill-name}/SKILL.md`
- [ ] Directory structure mirrors `.rulesync/skills/`
- [ ] Each skill has valid frontmatter

### Skill Content
- [ ] Skills include scope and out-of-scope sections
- [ ] Routing logic documented for complex skills
- [ ] Cross-references use `[skill:name]` syntax
- [ ] Read-only focus in guidance

### Skill Adaptations for Read-Only
- [ ] Skills don't instruct file modifications
- [ ] Skills focus on analysis and guidance
- [ ] Implementation steps marked as "for user to perform"

### Loading Test
```bash
# In Codex CLI:
codex "Load skill dotnet-advisor"
codex "Use skill dotnet-testing to analyze test strategy"
```
- [ ] Skills load without errors
- [ ] Skill content is accessible

---

## MCP Verification

### Location and Format
- [ ] `.codex/mcp.json` exists
- [ ] Valid JSON syntax
- [ ] Same structure as `.rulesync/mcp.json`

### MCP Servers
| Server | Type | Status |
|--------|------|--------|
| serena | stdio | [ ] |
| microsoftdocs-mcp | http | [ ] |

### MCP Connection Test
```bash
# In Codex CLI:
codex "Connect to serena MCP"
codex "Query microsoftdocs-mcp for ASP.NET Core documentation"
```
- [ ] MCP servers connect successfully
- [ ] Tools are listed and callable

---

## Ignore Verification

### Location
- [ ] `.codex/.codexignore` exists
- [ ] Content mirrors `.rulesync/.aiignore`

### Patterns Verified
- [ ] Build outputs ignored (bin/, obj/, *.dll)
- [ ] IDE files ignored (.vs/, .vscode/, *.user)
- [ ] Dependencies ignored (node_modules/, packages/)
- [ ] Test results ignored (TestResults/, coverage/)
- [ ] Secrets ignored (*.key, *.pem, .env)

---

## Sandbox Mode Guidelines

### Read-Only Mode (Default)
```yaml
codexcli:
  sandbox_mode: "read-only"
```

**Characteristics:**
- [ ] Agent can only read files
- [ ] No file creation allowed
- [ ] No file modification allowed
- [ ] Analysis and guidance only

### When to Use
- [ ] Code review agents
- [ ] Architecture analysis
- [ ] Documentation generation
- [ ] Security auditing

### Inherit Mode
```yaml
codexcli:
  sandbox_mode: inherit
```

**Characteristics:**
- [ ] Inherits sandbox from parent context
- [ ] Allows flexible permissions
- [ ] Use with caution

---

## Common Issues and Fixes

### Issue: Commands referenced but not supported
**Symptom:** Generated files contain command references

**Verification Steps:**
1. Check for command files in `.codex/commands/`
2. Verify commands not generated for codexcli
3. Check command guidance moved to rules

**Fix:** Remove command files and move guidance to rules

---

### Issue: Subagent without sandbox_mode
**Symptom:** Subagent doesn't specify read-only mode

**Verification Steps:**
1. Check agent frontmatter for `codexcli.sandbox_mode`
2. Verify default is `"read-only"`

**Fix:** Add sandbox_mode to frontmatter:
```yaml
codexcli:
  sandbox_mode: "read-only"
  short-description: 'Read-only .NET architecture analysis'
```

---

### Issue: Hooks referenced but not supported
**Symptom:** Rule content references hooks

**Verification Steps:**
1. Check for hook references in rules
2. Verify hooks not generated for codexcli
3. Check hook guidance moved to rules

**Fix:** Remove hook references from rules for codexcli

---

### Issue: Agent attempting writes
**Symptom:** Read-only agent tries to modify files

**Verification Steps:**
1. Check agent instructions don't include "edit", "write", "create"
2. Verify workflow is analysis-only
3. Check examples don't show modifications

**Fix:** Update agent content to be analysis-focused:
```markdown
## Workflow
1. Analyze the codebase structure
2. Identify issues and opportunities
3. Report findings to the user

Note: Do not modify files. Provide recommendations only.
```

---

### Issue: MCP tools not available
**Symptom:** MCP servers don't connect in Codex CLI

**Verification Steps:**
1. Check `.codex/mcp.json` syntax
2. Verify required tools installed (uvx)
3. Check API keys configured

**Fix:** Validate JSON and install dependencies

---

## Post-Verification Summary

### Critical Checks Passed
- [ ] Root rule (CODEX.md) valid and accessible
- [ ] No command files generated
- [ ] No hook configuration generated
- [ ] All subagents have `sandbox_mode: "read-only"`
- [ ] Core skills loadable
- [ ] MCP servers connectable

### Codex-Specific Verification
- [ ] Read-only focus throughout
- [ ] Command/hook guidance moved to rules
- [ ] Subagents appropriately sandboxed

### Issues Found
| Issue | Severity | Status |
|-------|----------|--------|
| | | |

### Notes
___________
___________
