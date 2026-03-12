# Factory Droid Verification Checklist

**Target ID:** `factorydroid`  
**Runtime Shape:** Rules, MCP, hooks  
**Special Behavior:** Do not depend on imported skills, subagents, or commands; route through generated rules  
**Verification Date:** ___________  
**Verified By:** ___________

---

## Expected Files Generated

### Primary Configuration
- [ ] `.factorydroid/FACTORYDROID.md` or similar root rule
- [ ] Root rule contains comprehensive guidance

### Rules Directory
- [ ] `.factorydroid/rules/*.md` - Individual rule files from `.rulesync/rules/`
- [ ] `.factorydroid/rules/factorydroid-overrides.md` - Target-specific overrides

### Rules-Only Delivery
- [ ] **NO** separate commands directory
- [ ] **NO** separate subagents directory
- [ ] **NO** separate skills directory
- [ ] All guidance delivered through rules

### MCP Configuration
- [ ] `.factorydroid/mcp.json` - MCP server configuration
- [ ] Contains same servers as `.rulesync/mcp.json`

### Hooks (via Generated Rules)
- [ ] Hooks defined in `.rulesync/hooks.json` under `factorydroid.hooks`
- [ ] Hook behavior delivered through rule text, not direct execution

---

## Feature Support Matrix

| Feature | Status | Notes |
|---------|--------|-------|
| Rules | Native | **Primary delivery mechanism** |
| Ignore Patterns | N/A | Uses standard ignore if available |
| Commands | Via Rules | Delivered as rule text, not commands/ |
| Subagents | Via Rules | Delivered as rule text, not agents/ |
| Skills | Via Rules | Delivered as rule text, not skills/ |
| Hooks | Via Rules | Delivered as rule reminders, not hooks.json |
| MCP | Native | Via `.factorydroid/mcp.json` |
| Tool Support | Standard | Standard Factory Droid tool set |

**Key Differences from Claude Code:**
- **Rules-only delivery** - No imported skills, subagents, or commands
- **Content in rules** - All guidance embedded in rule files
- **Hook text** - Hooks delivered as rule text, not executed
- **Comprehensive root rule** - Root rule must be self-contained

---

## Rules Verification

### Root Rule Location
- [ ] Root rule exists in Factory Droid format
- [ ] Contains comprehensive overview content
- [ ] Self-contained (no external dependencies)
- [ ] Includes all critical guidance inline

### Comprehensive Root Rule Content
The root rule MUST include:
- [ ] Overview of dotnet-agent-harness
- [ ] Skill catalog (embedded, not referenced)
- [ ] Command descriptions (embedded, not separate files)
- [ ] Subagent capabilities (embedded descriptions)
- [ ] MCP server information
- [ ] Workflow guidance
- [ ] Operating modes documentation

### Non-Root Rules
- [ ] Rules from `.rulesync/rules/` copied to `.factorydroid/rules/`
- [ ] Each rule has valid frontmatter
- [ ] Rules have `targets` including `'factorydroid'`

### Factory Droid-Specific Adaptations
- [ ] No `[skill:name]` references (skills not imported)
- [ ] No `[subagent:name]` references (subagents not imported)
- [ ] No `/command:name` references (commands not imported)
- [ ] All content embedded in rule text

---

## Commands Verification (Via Rules)

### Important: No commands/ Directory

Factory Droid does NOT have a commands/ directory. Instead:

1. **Command descriptions** embedded in rules
2. **Command usage** documented in root rule
3. **Command examples** provided as text

### Command Content in Rules
Verify these commands are described in rules:
- [ ] `dotnet-agent-harness:bootstrap` - Description and usage
- [ ] `dotnet-agent-harness:search` - Description and examples
- [ ] `dotnet-agent-harness:recommend` - Description and usage
- [ ] `dotnet-agent-harness:graph` - Description and examples

### Example Rule Content
```markdown
## Available Commands

### dotnet-agent-harness:bootstrap
Bootstrap the harness for your project.

Usage: Ask me to "bootstrap dotnet-agent-harness with targets claudecode,opencode"

Options:
- --profile: core, platform-native, or full
- --targets: comma-separated target list
- --run-rulesync: run generation after config

### dotnet-agent-harness:search
Search the skill catalog.

Usage: Ask me to "search for testing skills"
```

---

## Subagents Verification (Via Rules)

### Important: No agents/ Directory

Factory Droid does NOT have an agents/ directory. Instead:

1. **Subagent capabilities** described in rules
2. **Trigger lexicon** embedded in rule text
3. **Example prompts** provided as guidance

### Subagent Content in Rules
Verify these agents are described in rules:
- [ ] `dotnet-architect` - Architecture analysis capabilities
- [ ] `dotnet-testing-specialist` - Testing guidance capabilities
- [ ] `dotnet-security-reviewer` - Security audit capabilities
- [ ] `dotnet-performance-analyst` - Performance analysis capabilities

### Example Rule Content
```markdown
## Specialist Areas

### Architecture Analysis
When you need architecture guidance, I can:
- Analyze solution structure
- Recommend design patterns
- Evaluate framework choices
- Review project organization

Ask: "Review the architecture of this solution" or "What pattern should I use?"

### Testing Strategy
When you need testing help, I can:
- Recommend test types
- Design test architecture
- Review test coverage
- Suggest testing tools

Ask: "Help me design tests for this project"
```

---

## Skills Verification (Via Rules)

### Important: No skills/ Directory

Factory Droid does NOT have a skills/ directory. Instead:

1. **Skill content** embedded in rules
2. **Skill routing** described in rule text
3. **Skill catalog** included in root rule

### Skill Content in Rules
Verify skill categories are covered:
- [ ] Foundation skills (dotnet-advisor, version detection)
- [ ] Core C# skills (patterns, async, DI)
- [ ] Architecture skills (APIs, EF Core, patterns)
- [ ] Testing skills (xUnit, integration, strategy)
- [ ] UI skills (Blazor, MAUI, Uno)
- [ ] DevOps skills (containers, CI/CD, deployment)

### Skill Catalog in Root Rule
```markdown
## Skill Catalog

### 1. Foundation
- **dotnet-advisor**: Router for all .NET skills
- **dotnet-version-detection**: Detect TFM and SDK version
- **dotnet-project-analysis**: Understand solution structure

### 2. Core C#
- **dotnet-csharp-coding-standards**: Naming and conventions
- **dotnet-csharp-async-patterns**: async/await best practices
- **dotnet-csharp-dependency-injection**: MS DI patterns

[... additional categories ...]
```

---

## Hooks Verification (Via Rules)

### Important: Hook Text, Not Execution

Factory Droid hooks are NOT executed from hooks.json. Instead:

1. **Hook reminders** embedded in rule text
2. **Advisory content** provided as guidance
3. **No automatic execution**

### Hook Content in Rules
Verify these hooks are mentioned:
- [ ] sessionStart context injection
- [ ] post-edit Roslyn advisories
- [ ] post-edit formatting reminders
- [ ] Slopwatch advisories

### Example Rule Content
```markdown
## Session Start

When a session begins, I will:
1. Detect .NET project context
2. Inject dotnet-advisor routing
3. Identify project structure

## Post-Edit Reminders

After file modifications:
- For .cs files: Consider running `dotnet format`
- For .csproj files: Consider running `dotnet restore`
- For test files: Consider running `dotnet test`

## Quality Advisories

I will watch for:
- Disabled tests (skipped, commented out)
- Suppressed warnings (#pragma, .editorconfig)
- TODO comments without issues
```

---

## MCP Verification

### Location and Format
- [ ] `.factorydroid/mcp.json` exists
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

---

## Common Issues and Fixes

### Issue: Separate directories generated
**Symptom:** commands/, agents/, or skills/ directories exist

**Verification Steps:**
1. Check `.factorydroid/` structure
2. Verify only rules/ and mcp.json exist
3. Check no subdirectory imports

**Fix:** Remove non-rules directories; regenerate with rules-only flag

---

### Issue: External references in rules
**Symptom:** Rules reference `[skill:name]` or `[subagent:name]`

**Verification Steps:**
1. Check rule content for external references
2. Verify content is embedded
3. Check for broken cross-references

**Fix:** Replace references with embedded content:
```markdown
<!-- Before -->
Load [skill:dotnet-advisor] for routing.

<!-- After -->
I will route your .NET queries to the appropriate specialist guidance below.
```

---

### Issue: Root rule too minimal
**Symptom:** Root rule doesn't contain comprehensive guidance

**Verification Steps:**
1. Check root rule length and content
2. Verify skill catalog included
3. Check command descriptions present

**Fix:** Expand root rule to be self-contained:
- Add full skill catalog
- Include command descriptions
- Embed subagent capabilities
- Add workflow guidance

---

### Issue: Hook execution expected
**Symptom:** Rules assume automatic hook execution

**Verification Steps:**
1. Check for "will automatically" language
2. Verify hook reminders are advisory only
3. Check no dependencies on hook execution

**Fix:** Change to advisory language:
```markdown
<!-- Before -->
The sessionStart hook will detect your project.

<!-- After -->
I will attempt to detect your project context. You may also tell me about your project.
```

---

### Issue: MCP configuration missing
**Symptom:** No mcp.json in .factorydroid/

**Verification Steps:**
1. Check `.factorydroid/mcp.json` exists
2. Verify valid JSON syntax
3. Check servers defined

**Fix:** Ensure MCP config generated for factorydroid target

---

## Post-Verification Summary

### Critical Checks Passed
- [ ] Root rule comprehensive and self-contained
- [ ] No commands/ directory
- [ ] No agents/ directory
- [ ] No skills/ directory
- [ ] MCP configuration present
- [ ] All guidance embedded in rules

### Factory Droid-Specific Verification
- [ ] Skill catalog embedded in root rule
- [ ] Command descriptions in rule text
- [ ] Subagent capabilities described
- [ ] Hook reminders advisory only
- [ ] No external references

### Issues Found
| Issue | Severity | Status |
|-------|----------|--------|
| | | |

### Notes
___________
___________
