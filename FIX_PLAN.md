# Fix Plan: dotnet-agent-harness Major Surgery

## Executive Summary

This codebase needs aggressive consolidation. 175 skills is unsustainable. Target: 30 high-quality, tested skills with automated validation.

---

## Phase 1: Immediate Fixes (This Week)

### 1.1 Remove Committed .bak Files

```bash
find .rulesync/skills -name "*.bak" -delete
git add -A
git commit -m "Remove committed .bak files"
```

**Why**: Sloppy, unprofessional, wastes space.

### 1.2 Generate Skill Catalog

Create `.rulesync/catalog.json`:

```json
{
  "version": "1.0.0",
  "skills": [
    {
      "name": "dotnet-testing",
      "category": "testing",
      "subcategory": "fundamentals",
      "complexity": "beginner",
      "path": ".rulesync/skills/dotnet-testing/SKILL.md",
      "tags": ["xunit", "unit-test"],
      "related": ["dotnet-testing-advanced"],
      "line_count": 305,
      "has_tests": false
    }
  ]
}
```

Add CI step to validate catalog is up to date:

```yaml
- name: Validate skill catalog
  run: |
    dotnet run -- catalog --validate
    git diff --exit-code .rulesync/catalog.json || exit 1
```

**Why**: O(1) lookup vs O(n) filesystem traversal.

### 1.3 Implement Automated Skill Testing

Create `.rulesync/tests/test-skills.yml`:

```yaml
name: Skill Validation Tests
tests:
  - name: All skills have valid frontmatter
    command: |
      for skill in .rulesync/skills/*/SKILL.md; do
        yq '.name' "$skill" > /dev/null || exit 1
      done
  
  - name: No TODO/FIXME in shipped skills
    command: |
      ! grep -r "TODO\|FIXME" .rulesync/skills/*/SKILL.md
  
  - name: Skills reference valid other skills
    command: |
      dotnet run -- skills --validate-refs
```

**Why**: Catch broken skills before shipping.

---

## Phase 2: Skill Consolidation (Next Week)

### 2.1 Merge Testing Skills

Current state: 19 testing-related skills

Target structure:
```
.rulesync/skills/
├── testing/
│   ├── SKILL.md          (merged: dotnet-testing + dotnet-testing-advanced)
│   ├── unit/
│   │   └── SKILL.md      (merged: fundamentals + xunit + naming)
│   ├── integration/
│   │   └── SKILL.md      (merged: integration + testcontainers + aspire)
│   ├── assertions/
│   │   └── SKILL.md      (merged: awesome-assertions + fluentvalidation)
│   ├── mocking/
│   │   └── SKILL.md      (merged: nsubstitute + autofixture)
│   └── advanced/
│       └── SKILL.md      (remaining advanced topics)
```

Merge strategy:
1. Keep dotnet-integration-testing as base (750 lines, most content)
2. Merge unique content from dotnet-testing and dotnet-testing-advanced
3. Delete merged skills
4. Update references in subagents

**Expected**: 19 → 6 skills

### 2.2 Merge Security Skills

Current: `dotnet-security`, `dotnet-security-owasp`

Merge into single `security/SKILL.md` with sections:
- OWASP Top 10
- Secure coding patterns
- Authentication/Authorization
- Cryptography

**Expected**: 2 → 1 skill

### 2.3 Merge Architecture Skills

Current: `dotnet-architecture`, `dotnet-architecture-patterns`, `dotnet-domain-modeling`

Merge into `architecture/SKILL.md`:
- Architectural patterns
- Vertical slices
- Domain modeling

**Expected**: 3 → 1 skill

### 2.4 Consolidate EF Core Skills

Current: `dotnet-efcore-patterns`, `dotnet-efcore-architecture`

Merge into `data/ef-core/SKILL.md`

**Expected**: 2 → 1 skill

### 2.5 Consolidate Blazor Skills

Current: `dotnet-blazor-auth`, `dotnet-blazor-components`, `dotnet-blazor-patterns`, `dotnet-blazor-testing`

Merge into `blazor/SKILL.md`

**Expected**: 4 → 1 skill

### 2.6 Delete or Archive Low-Value Skills

Skills to remove:
- `dotnet-agent-harness-*` (meta skills, not user-facing)
- Duplicate pattern skills
- Obsolete version-specific skills (older TFM guidance)

**Expected**: Remove 50+ skills

---

## Phase 3: Validation & Testing (Week 3)

### 3.1 Delete Manual Verification Checklists

```bash
rm -rf .rulesync/verification/
git add -A
git commit -m "Remove manual verification checklists - replaced with automated tests"
```

**Why**: Documentation theater. Not maintained. Not used.

### 3.2 Standardize Frontmatter

Create `.rulesync/schema/skill-frontmatter.json`:

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "type": "object",
  "required": ["name", "category", "subcategory", "description"],
  "properties": {
    "name": { "type": "string", "pattern": "^[a-z0-9-]+$" },
    "category": { "enum": ["fundamentals", "testing", "architecture", "web", "data", "performance", "security", "devops", "platforms", "tooling"] },
    "subcategory": { "type": "string" },
    "description": { "type": "string", "maxLength": 160 },
    "complexity": { "enum": ["beginner", "intermediate", "advanced"] },
    "tags": { "type": "array", "items": { "type": "string" } }
  }
}
```

Add CI validation:

```yaml
- name: Validate skill frontmatter
  run: |
    for skill in .rulesync/skills/*/SKILL.md; do
      yq -o json "$skill" | jq '.[0]' | \
        jsonschema -i /dev/stdin .rulesync/schema/skill-frontmatter.json
    done
```

### 3.3 Add Skill Loading Tests

```csharp
// Test that all skills load without errors
[Fact]
public void AllSkills_ShouldLoadSuccessfully()
{
    var loader = new SkillLoader(".rulesync/skills");
    var skills = loader.LoadAll();
    
    foreach (var skill in skills)
    {
        skill.Frontmatter.Should().NotBeNull();
        skill.Frontmatter.Name.Should().NotBeNullOrEmpty();
    }
}
```

---

## Phase 4: Build System Cleanup (Week 4)

### 4.1 Generate AI Tool Files at Build Time

Modify `.rulesync/rulesync.jsonc`:

```jsonc
{
  "output": {
    "targets": [
      {
        "name": "claudecode",
        "path": "generated/claudecode/",  // Not committed
        "files": ["AGENTS.md", "CLAUDE.md"]
      }
    ]
  }
}
```

Add to `.gitignore`:
```
generated/
.claude/
.gemini/
.opencode/
.cursor/
.windsurf/
.kilocode/
.factory/
.copilot/
.codex/
.antigravity/
```

### 4.2 Update CI to Generate Files

```yaml
- name: Generate AI tool configurations
  run: rulesync generate --all
  
- name: Upload generated files as artifacts
  uses: actions/upload-artifact@v4
  with:
    name: ai-tool-configs
    path: generated/
```

### 4.3 Create Release Package

```yaml
- name: Create release package
  run: |
    rulesync generate --all
    tar -czf dotnet-agent-harness-configs.tar.gz generated/
    
- name: Upload to release
  uses: softprops/action-gh-release@v1
  with:
    files: dotnet-agent-harness-configs.tar.gz
```

**Why**: Stop committing generated files.

---

## Phase 5: Documentation (Ongoing)

### 5.1 Create Skill Writing Guide

`docs/skill-authoring.md`:
- Frontmatter requirements
- Content structure (max 500 lines)
- Example quality standards
- Testing requirements

### 5.2 Update README

Current README is TL;DR. Make it:
- What this toolkit does (2 sentences)
- Quick install (1 command)
- Usage (2 examples)
- Link to full docs

---

## Expected Outcomes

| Metric | Before | After | Change |
|--------|--------|-------|--------|
| Skills | 175 | 30 | -83% |
| Skill Lines | ~70,000 | ~15,000 | -79% |
| Verification Lines | 2,247 | 0 | -100% |
| Generated Files | ~45 | 0 | -100% |
| Test Coverage | ~0% | 80% | +80% |
| CI Time | ~5 min | ~3 min | -40% |

---

## Risk Mitigation

### Breaking Changes
- Tag current state as `v1.0-legacy`
- Maintain compatibility layer for 3 months
- Announce breaking changes in CHANGELOG

### Skill Removal
- Archive removed skills to `.archived/skills/`
- Provide migration guide for merged skills
- Keep redirects for 6 months

### User Impact
- Generated files move to artifacts (not breaking)
- Skills consolidate (breaking for direct references)
- Test framework changes (minor breaking)

---

## Success Criteria

1. [ ] CI passes with skill validation tests
2. [ ] Catalog.json generates automatically
3. [ ] No TODO comments in shipped skills
4. [ ] All skills < 500 lines
5. [ ] No committed generated files
6. [ ] 80% test coverage
7. [ ] PR reviews take < 10 minutes (small changes)

---

*This is a 4-week plan to transform a bloated mess into a maintainable toolkit.*
