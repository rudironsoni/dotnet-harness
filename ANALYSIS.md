# Codebase Analysis: dotnet-agent-harness

## Verdict: NAK - This Needs Major Surgery

This codebase exhibits classic signs of over-engineering without sufficient rigor. 175 skills with minimal testing, massive duplication, and generated file pollution that creates maintenance hell.

---

## Critical Issues (Must Fix)

### 1. Skill Architecture is Broken

**Problem**: 175 skills in a flat namespace with O(n) lookup and no catalog.

**Evidence**:
- Skills like `dotnet-testing`, `dotnet-testing-advanced`, `dotnet-integration-testing`, `dotnet-testing-advanced-aspnet-integration-testing` all cover overlapping topics
- Frontmatter is inconsistent (some use `metadata:`, some don't; some have `invocable`, some don't)
- No taxonomy enforcement beyond directory names
- Skills average 400+ lines each - many are essay-length

**Impact**: 
- Skills can't be discovered programmatically
- Duplicate content leads to maintenance drift
- Router can't make intelligent decisions

**Fix**: Consolidate to 25-30 high-quality skills with clear taxonomy

### 2. Zero Automated Skill Testing

**Problem**: Test framework exists but isn't used.

**Evidence**:
- `dotnet-agent-harness-test-framework` has ONE test case with 3 assertions
- CI runs shellcheck and dotnet build, but no skill validation
- No integration tests for RuleSync generation
- Skills have TODO comments in shipped content

**Impact**:
- Broken skills ship to users
- No regression protection
- Manual verification is unsustainable

**Fix**: Implement automated skill testing in CI

### 3. Generated File Pollution

**Problem**: 15+ AI tool directories with duplicate AGENTS.md files.

**Evidence**:
- `.claude/`, `.gemini/`, `.opencode/`, `.cursor/`, `.windsurf/`, `.kilocode/`, etc.
- Each contains the same content copied from templates
- No verification these are in sync

**Impact**:
- One change requires updating 15+ files
- Guaranteed drift over time
- Repository bloat

**Fix**: Generate at build time, don't commit

### 4. Manual Verification Theater

**Problem**: 2000+ lines of manual checklists that aren't automated.

**Evidence**:
- `verification/` directory has 321-line checklists per target (7 targets = 2,247 lines)
- Checklists have TODO comments
- CI doesn't validate against them

**Impact**:
- Documentation waste
- Gives false sense of security
- Never actually used

**Fix**: Delete or automate

### 5. No Skill Index/Catalog

**Problem**: No machine-readable index of skills.

**Evidence**:
- No `index/skills.json` or similar
- `rulesync.jsonc` doesn't catalog skills
- Discovery requires filesystem traversal

**Impact**:
- Router can't make informed decisions
- No programmatic skill lookup
- Human-only discovery

**Fix**: Generate index during build

---

## Secondary Issues (Should Fix)

### 6. Committed .bak Files
- `.rulesync/skills/*/SKILL.md.bak` files in git
- Sloppy cleanup

### 7. TODO Comments in Skills
- 8+ skills have TODO/FIXME in SKILL.md
- Shipping unfinished work

### 8. Inconsistent Documentation
- Some skills are 750+ line essays
- Others are 100-line stubs
- No length guidance

### 9. Missing Integration Tests
- RuleSync generation isn't tested end-to-end
- No validation that generated files work

---

## Data Structure Problems

### Current Anti-Patterns

1. **Flat skill directory**: 175 items in one folder
2. **Text-based frontmatter**: YAML parsing on every access
3. **No relationship graph**: Skills can't reference dependencies
4. **Generated file explosion**: N targets * M files = O(N*M) storage
5. **Manual checklist burden**: O(T) checklists for T targets

### Better Approach

```
.rulesync/
├── catalog.json           # Machine-readable skill index
├── skills/                # 25-30 consolidated skills
│   ├── testing/          # One skill per domain
│   ├── architecture/
│   └── ...
├── generated/            # Build outputs (not committed)
│   └── {target}/
│       └── AGENTS.md
└── verification/         # Automated tests (not checklists)
    └── test-cases/
```

---

## Performance Issues

1. **Skill lookup**: O(n) filesystem traversal
2. **RuleSync generation**: O(skills * targets) with no caching
3. **Memory**: Loading 175 SKILL.md files into context

---

## Recommended Priority Order

### Phase 1: Foundation (Week 1)
1. Generate skill index/catalog.json
2. Implement automated skill testing in CI
3. Remove committed .bak files

### Phase 2: Consolidation (Week 2)
1. Merge duplicate testing skills
2. Delete manual verification checklists
3. Standardize frontmatter

### Phase 3: Cleanup (Week 3)
1. Move generated files to build-time only
2. Remove TODOs from skills
3. Add integration tests

---

## Metrics

- **Skills**: 175 → Target: 30 (83% reduction)
- **Lines of verification**: 2,247 → Target: 0 (automated)
- **Generated files committed**: ~45 → Target: 0
- **Test coverage**: ~0% → Target: 80%

---

*This is a toolkit that wants to be comprehensive but lacks the rigor to maintain quality at scale.*
