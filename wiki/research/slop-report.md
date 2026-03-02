---
author: automated-scribe
date: 2026-03-01
---

# Slop Report — Automated Slopwatch Research

This report summarizes findings from the Slopwatch analysis and related repository evidence. It is intended to help
reviewers and maintainers eliminate "slop" (shortcuts that hide real problems) such as disabled tests, blanket warning
suppression, empty catch blocks, and CI/CSProj workarounds.

Source evidence is drawn from the repository's guidance and skill files (noted inline). Where possible exact file paths
and line numbers are provided so reviewers can jump straight to the code.

## Executive summary — Top 10 findings

1. Disabled / skipped tests detected (SW001) — tests hidden with [Fact(Skip=...)], commented-out attributes, or #if
   false. Example evidence: .rulesync/skills/dotnet-solution-navigation/SKILL.md (lines 665-676).
2. Blanket project warning suppressions (`<NoWarn>`) or TreatWarningsAsErrors disabled (SW005) — hides compiler/analysis
   issues. Evidence: .rulesync/skills/dotnet-csproj-reading/SKILL.md (lines 599-605).
3. Warning suppression via `#pragma warning disable` or SuppressMessage (SW002) — frequently used in skill examples and
   some guidance files; risky when broad. Evidence: .rulesync/skills/dotnet-xml-docs/SKILL.md (line 86).
4. Empty/ swallowing catch blocks (SW003) — `catch { }` or broad swallow patterns hide failures. Evidence: dotnet
   anti-pattern guidance (.rulesync/skills/dotnet-10-csharp-14/anti-patterns.md, line 432) and Slopwatch rules
   (dotnet-slopwatch SKILL.md lines 185-193).
5. Arbitrary delays in tests (SW004) — Thread.Sleep/Task.Delay used to mask flakiness. Evidence: dotnet-slopwatch
   SKILL.md (line 190) and test guidance in .rulesync skills.
6. Commented-out tests and attributes — common in quick patches. Evidence:
   .rulesync/skills/dotnet-solution-navigation/SKILL.md (lines 678-681).
7. Baseline misuse: adding existing slop to baseline without justification — documented in Slopwatch guidance
   (.rulesync/skills/dotnet-slopwatch/SKILL.md lines 112-119 and 122-131).
8. CPM / project metadata bypass (SW006) — VersionOverride/inline Version attributes can hide dependency mismatch
   issues. Evidence: Slopwatch rules (dotnet-slopwatch SKILL.md lines 191-192).
9. Local tool/CI install skipped or brittle tool restore patterns — causes CI drift and latent breakage. Evidence:
   justfile (lines 17-19, 65-71) and dotnet-tool guidance in dotnet-slopwatch.
10. Missing TreatWarningsAsErrors in project files (CI parity risk) — example guidance:
    .rulesync/skills/dotnet-csproj-reading/SKILL.md (lines 182-190).

Each item below includes the full evidence table, remediation suggestions, and an actionable roadmap.

## Full evidence table

Notes: "Severity" uses Slopwatch severity conventions (Error / Warning / Info). "File:line" references the repository
file and the line number shown in the skills/source files where the pattern or example appears.

- Finding 1: Disabled/skipped tests
  - Category: Disabled tests (SW001)
  - File:line: .rulesync/skills/dotnet-solution-navigation/SKILL.md:665-676
  - 3-line code snippet:

```csharp
// RED FLAG: skipped tests that will not run during dotnet test
[Fact(Skip = "Flaky -- revisit later")]
public async Task ProcessOrder_ConcurrentRequests_HandledCorrectly() { }
```

- Severity: Error
- One-line impact: Tests are invisible to CI — regressions will go undetected.
- Suggested remediation: Re-enable the test and fix flakiness, or convert to a documented skip with a tracking issue and
  a short-term mitigation (e.g., retry logic), then create a follow-up PR to resolve root cause.

- Finding 2: Blanket NoWarn entries in .csproj
  - Category: Project-file slop (SW005)
  - File:line: .rulesync/skills/dotnet-csproj-reading/SKILL.md:599-605
  - 3-line code snippet:

```xml
<!-- RED FLAG: blanket NoWarn in .csproj -->
  <NoWarn>CS8600;CS8602;CS8603;CS8604;IL2026;IL2046</NoWarn>
```

- Severity: Warning
- One-line impact: Suppresses whole classes of warnings, hiding real issues from CI and maintainers.
- Suggested remediation: Remove blanket <NoWarn> entries; replace with targeted analyzer suppression in .editorconfig or
  per-file #pragma with justification comments.

- Finding 3: #pragma warning disable (broad suppression)
  - Category: Warning suppression (SW002)
  - File:line: .rulesync/skills/dotnet-xml-docs/SKILL.md:86
  - 3-line code snippet:

```csharp
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// (used in examples to illustrate suppression patterns)
#pragma warning restore CS1591
```

- Severity: Warning
- One-line impact: Can mask important diagnostics (nullability, API surface, trimming warnings) if used broadly.
- Suggested remediation: Narrow suppression to minimal scope (single `#pragma` + restore), document justification
  inline, or prefer .editorconfig severity overrides.

- Finding 4: Empty catch blocks
  - Category: Empty catch / swallowing exceptions (SW003)
  - File:line: .rulesync/skills/dotnet-10-csharp-14/anti-patterns.md:432 (anti-pattern list) and dotnet-slopwatch rules:
    .rulesync/skills/dotnet-slopwatch/SKILL.md:189-193
  - 3-line code snippet:

```csharp
try {
    DoSomething();
} catch { }
```

- Severity: Error
- One-line impact: Hides runtime errors and causes silent failures or data loss.
- Suggested remediation: Log and rethrow, or handle specific exception types; if intentionally swallowing, add a
  detailed comment and consider a metric/telemetry increment.

- Finding 5: Arbitrary delays in tests (Task.Delay / Thread.Sleep)
  - Category: Arbitrary delays (SW004)
  - File:line: .rulesync/skills/dotnet-slopwatch/SKILL.md:190
  - 3-line code snippet (representative):

```csharp
// RED FLAG: arbitrary delay in test to avoid race condition
await Task.Delay(1000);
// Replace with explicit synchronization or retry logic
```

- Severity: Warning
- One-line impact: Tests take longer and may still be flaky; masks real concurrency issues.
- Suggested remediation: Replace with deterministic coordination (e.g., wait for a condition, use
  retry/assert-with-timeout patterns, or mocked timing primitives).

- Finding 6: Commented-out tests and attributes
  - Category: Commented-out test code
  - File:line: .rulesync/skills/dotnet-solution-navigation/SKILL.md:678-681
  - 3-line code snippet:

```csharp
// [Fact]
// public void CalculateDiscount_NegativeAmount_ThrowsException() { }
```

- Severity: Error
- One-line impact: Hidden test logic that may be required for coverage or regression detection.
- Suggested remediation: Re-enable and fix, or remove with a clear changelog entry and issue reference.

- Finding 7: Baseline misuse / blind baseline updates
  - Category: Baseline/configuration (policy)
  - File:line: .rulesync/skills/dotnet-slopwatch/SKILL.md:112-119 and 122-131
  - 3-line code snippet:

```bash
slopwatch init
git add .slopwatch/baseline.json
git commit -m "Add slopwatch baseline"
```

- Severity: Warning
- One-line impact: Baseline becomes a permanent "dumping ground" of problems if updated without strong justification.
- Suggested remediation: Only create/update baseline with PRs that include justification, code comments, and reviewer
  sign-off; avoid bulk acceptance.

- Finding 8: CPM / VersionOverride bypass (SW006)
  - Category: CPM/project metadata bypass (SW006)
  - File:line: .rulesync/skills/dotnet-slopwatch/SKILL.md:191-192
  - 3-line code snippet (representative):

```xml
<!-- Inline Version or VersionOverride used to bypass central package management -->
<PackageReference Include="Foo" Version="1.2.3" />
```

- Severity: Warning
- One-line impact: Masks dependency drift and prevents consistent updates across projects.
- Suggested remediation: Use Central Package Management (Directory.Packages.props) or maintain documented exceptions
  with justification and follow-up PRs.

- Finding 9: CI hook/tool installation inconsistencies
  - Category: CI weaknesses
  - File:line: .rulesync/skills/dotnet-slopwatch/SKILL.md:252-271 and justfile lines 17-19, 65-71
  - 3-line code snippet (GH Actions snippet):

```yaml
- name: Install Slopwatch
  run: dotnet tool install --global Slopwatch.Cmd
- name: Run Slopwatch
  run: slopwatch analyze -d . --fail-on warning
```

- Severity: Warning
- One-line impact: Missing tool installation or skipped restore steps lead to nondeterministic pipeline behavior.
- Suggested remediation: Use pinned local tool manifest (.config/dotnet-tools.json) and `dotnet tool restore` as part of
  CI; prefer local tool install in CI jobs for reproducibility.

- Finding 10: Missing TreatWarningsAsErrors (CI parity)
  - Category: Build/CI configuration
  - File:line: .rulesync/skills/dotnet-csproj-reading/SKILL.md:182-190
  - 3-line code snippet:

```xml
<PropertyGroup>
  <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
</PropertyGroup>
```

- Severity: Warning
- One-line impact: Warnings can drift in developer machines vs CI unless enforced.
- Suggested remediation: Add TreatWarningsAsErrors=true (or enable for CI via environment) and remediate existing
  warnings incrementally.

## Prioritized remediation roadmap (PRs)

The roadmap is organized as small, reviewable PRs that minimize risk. Estimated effort is shown in developer hours
(small = 1-4h, medium = 4-12h, large = 1-3d) and risk (Low/Medium/High).

1. PR: enforce Slopwatch in CI (low risk)
   - Files: .github/workflows/\* (add job), .config/dotnet-tools.json (pin Slopwatch)
   - Changes: add Slopwatch job using local tool restore and `slopwatch analyze --fail-on warning`
   - Effort: small (2-4h)
   - Risk: Low

2. PR: audit and remove blanket <NoWarn> entries (medium risk)
   - Files: list of project files where <NoWarn> is used (use grep to find)
   - Changes: remove blanket NoWarn; replace with targeted .editorconfig entries or per-file #pragma with justification
   - Effort: medium (1-2 dev days depending on findings)
   - Risk: Medium (may surface many warnings requiring incremental fixes)

3. PR: fix top disabled/skipped tests (higher priority, incremental)
   - Files: test projects found by grep of 'Skip' and '#if false'
   - Changes: Re-enable tests, add deterministic fixes (mocks, retry, explicit wait-for conditions) or convert to
     documented skips with tracking issues
   - Effort: per-test small; overall medium (depends on number)
   - Risk: Medium (tests may fail until fixed)

4. PR: remove empty catch blocks and add proper handling (medium)
   - Files: candidate files found by searching for 'catch { }' (use serena/grep)
   - Changes: add logging, rethrow or handle specific exceptions, or add justification comment + telemetry
   - Effort: medium (4-12h)
   - Risk: Medium

5. PR: restrict and document any remaining suppressions (low)
   - Files: places using `#pragma warning disable`, `SuppressMessage`, or perf suppressions
   - Changes: narrow scope, add detailed justification comments, convert to .editorconfig where appropriate
   - Effort: small (2-6h)
   - Risk: Low

6. PR: baseline review (policy) — add governance for `.slopwatch/baseline.json` updates (low)
   - Files: .slopwatch/slopwatch.json and CONTRIBUTING or docs
   - Changes: Add required PR template text for baseline updates and require reviewer sign-off
   - Effort: small (2-4h)
   - Risk: Low

Suggested sequencing: 1 (CI) → 6 (baseline policy) → 3 (tests) → 2 (NoWarn) → 4 (catch) → 5 (suppressions).

## CI gate recipes and detection regexes

Paste-ready GitHub Actions job (copy/paste):

```yaml
jobs:
  slopwatch:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'
      - name: Restore tools
        run: dotnet tool restore
      - name: Run Slopwatch
        run: slopwatch analyze -d . --fail-on warning
```

Key detection regexes and discovery commands (useable in CI or locally):

- Find skipped tests (xUnit attributes):

Regex: Skip\s*=\s*"._" (grep form: grep -rEn "Skip[[:space:]]_=\s*\"" --include="*.cs" .)

- Find `#if false` that hides entire/test classes:

Command: grep -rn "#if false" --include="\*.cs" . | grep -v "obj/" | grep -v "bin/"

- Find commented test attributes:

Command: grep -rEn '//[[:space:]]_\[(Fact|Theory|Test)\]' --include="_.cs" . | grep -v "obj/"

- Find broad pragma suppression:

Regex: #pragma\s+warning\s+disable

- Find blanket NoWarn in csproj files:

Regex (xml): <NoWarn>.\*<\/NoWarn>

- Find empty catch blocks:

Regex: catch\s*\{\s*\}

- Find Task.Delay/Thread.Sleep in tests (heuristic):

Regex: (Task\.Delay|Thread\.Sleep)\s\*\(

Run Slopwatch CLI in CI with the `--hook` flag for fast checks over changed files (recommended for code hooks):

Command: slopwatch analyze -d . --hook

## Quick commands and reviewer checklist

Quick commands (local):

- Initialize baseline (first-run only):
  - slopwatch init

- Run full analysis
  - slopwatch analyze --output json --fail-on warning

- Run fast hook mode (changed files only)
  - slopwatch analyze -d . --hook

- Find skipped tests locally
  - grep -rEn 'Skip[[:space:]]_=\s_"' --include="\*.cs" . | grep -v "obj/"

- Find pragma suppressions
  - grep -rEn "#pragma warning disable" --include="\*.cs" . | grep -v "obj/"

Reviewer checklist (use when reviewing PRs):

1. Does this PR add or remove any [Fact]/[Theory] Skip attributes? If yes, is there a tracking issue and a short-term
   plan? If tests are re-enabled, ensure they pass locally.
2. Are any <NoWarn> entries added/removed? If added, is there an inline justification and a follow-up issue to remediate
   the suppressed warnings?
3. Are there any `#pragma warning disable` usages? Ensure scope is minimal and a justification comment exists.
4. Do any changes introduce empty catch blocks? If so, require logging or specific exception handling.
5. Does CI run Slopwatch (or equivalent) as part of the pipeline? If not, request adding the job.
6. If baseline updates were included (.slopwatch/baseline.json), check the PR description for explicit justification and
   reviewer sign-off.

## Appendix — References

- Slopwatch skill & guidance (primary): .rulesync/skills/dotnet-slopwatch/SKILL.md (see detection rules and usage
  examples) — example lines: 185-193, 112-119, 252-271.
- Solution navigation Slopwatch anti-pattern examples: .rulesync/skills/dotnet-solution-navigation/SKILL.md (lines
  655-676, 688-695).
- CSProj guidance and NoWarn discussion: .rulesync/skills/dotnet-csproj-reading/SKILL.md (lines ~580-616; see NoWarn
  examples and TreatWarningsAsErrors guidance lines 182-190).
- Pragma / suppression examples: .rulesync/skills/dotnet-xml-docs/SKILL.md (line 86).
- Anti-patterns reference (catch {}): .rulesync/skills/dotnet-10-csharp-14/anti-patterns.md (line 432).

If you want, I can: (a) create the suggested CI job commit, (b) run a repository-wide grep to produce a targeted list of
files for each finding, or (c) open the first PR to enable Slopwatch in CI with a pinned local tool manifest.

---

Generated by automated-scribe on 2026-03-01. Reviewers: please use the checklist above and link each remediation PR back
to the finding ID (e.g., SW001, SW002).
