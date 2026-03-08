#!/usr/bin/env bash
# Validation script for RuleSync-first infrastructure
# Checks skill count, command count, subagent count, JSON files, and hook scripts

set -euo pipefail

# Configuration - expected counts
readonly EXPECTED_SKILL_COUNT=193
readonly EXPECTED_COMMAND_COUNT=27
readonly EXPECTED_SUBAGENT_COUNT=18

# Error tracking
ERRORS=0
WARNINGS=0

# Log helpers
log_info() {
    echo "[INFO] $*"
}

log_pass() {
    echo "[PASS] $*"
}

log_fail() {
    echo "[FAIL] $*"
    ((ERRORS++))
}

log_warn() {
    echo "[WARN] $*"
    ((WARNINGS++))
}

# Early exit: ensure required directories exist
if [[ ! -d "/workspace/.rulesync" ]]; then
    log_fail ".rulesync directory not found"
    exit 1
fi

cd /workspace/.rulesync

echo "=========================================="
echo "RuleSync Validation Suite"
echo "=========================================="

# 1. Validate skill count
echo ""
echo "--- Skill Count Validation ---"
SKILL_COUNT=$(find skills -mindepth 1 -maxdepth 1 -type d ! -name ".*" | wc -l)
log_info "Found $SKILL_COUNT skills (expected: $EXPECTED_SKILL_COUNT)"

if [[ "$SKILL_COUNT" -eq "$EXPECTED_SKILL_COUNT" ]]; then
    log_pass "Skill count matches expected value"
else
    log_fail "Skill count mismatch: got $SKILL_COUNT, expected $EXPECTED_SKILL_COUNT"
fi

# 2. Validate command count
echo ""
echo "--- Command Count Validation ---"
if [[ -d "commands" ]]; then
    # Count .md files excluding PORTABILITY.md
    COMMAND_COUNT=$(find commands -maxdepth 1 -name "*.md" ! -name "PORTABILITY.md" | wc -l)
    log_info "Found $COMMAND_COUNT commands (expected: $EXPECTED_COMMAND_COUNT)"

    if [[ "$COMMAND_COUNT" -eq "$EXPECTED_COMMAND_COUNT" ]]; then
        log_pass "Command count matches expected value"
    else
        log_fail "Command count mismatch: got $COMMAND_COUNT, expected $EXPECTED_COMMAND_COUNT"
    fi
else
    log_fail "commands directory not found"
fi

# 3. Validate subagent count
echo ""
echo "--- Subagent Count Validation ---"
if [[ -d "subagents" ]]; then
    SUBAGENT_COUNT=$(find subagents -maxdepth 1 -name "*.md" | wc -l)
    log_info "Found $SUBAGENT_COUNT subagents (expected: $EXPECTED_SUBAGENT_COUNT)"

    if [[ "$SUBAGENT_COUNT" -eq "$EXPECTED_SUBAGENT_COUNT" ]]; then
        log_pass "Subagent count matches expected value"
    else
        log_fail "Subagent count mismatch: got $SUBAGENT_COUNT, expected $EXPECTED_SUBAGENT_COUNT"
    fi
else
    log_fail "subagents directory not found"
fi

# 4. Validate JSON files
echo ""
echo "--- JSON File Validation ---"

# Validate hooks.json
if [[ -f "hooks.json" ]]; then
    if jq empty hooks.json 2>/dev/null; then
        log_pass "hooks.json is valid JSON"
    else
        log_fail "hooks.json contains invalid JSON"
    fi
else
    log_warn "hooks.json not found"
fi

# Validate mcp.json
if [[ -f "mcp.json" ]]; then
    if jq empty mcp.json 2>/dev/null; then
        log_pass "mcp.json is valid JSON"
    else
        log_fail "mcp.json contains invalid JSON"
    fi
else
    log_warn "mcp.json not found"
fi

# Validate template.json files
TEMPLATE_ERRORS=0
while IFS= read -r -d '' template_file; do
    if ! jq empty "$template_file" 2>/dev/null; then
        log_fail "Invalid JSON in $template_file"
        ((TEMPLATE_ERRORS++))
    fi
done < <(find templates -name "template.json" -print0 2>/dev/null || true)

if [[ $TEMPLATE_ERRORS -eq 0 ]]; then
    log_pass "All template.json files are valid"
fi

# 5. Check hook scripts
echo ""
echo "--- Hook Script Validation ---"

if [[ -d "hooks" ]]; then
    HOOK_ERRORS=0
    while IFS= read -r -d '' hook_script; do
        # Check if file is executable
        if [[ ! -x "$hook_script" ]]; then
            log_warn "Hook script not executable: $hook_script"
        fi

        # Run shellcheck on bash scripts
        if head -1 "$hook_script" | grep -q "bash"; then
            if shellcheck "$hook_script" > /dev/null 2>&1; then
                log_pass "Shellcheck passed for $(basename "$hook_script")"
            else
                log_warn "Shellcheck warnings in $(basename "$hook_script")"
            fi
        fi
done < <(find hooks -type f -name "*.sh" -print0 2>/dev/null || true)

    if [[ $HOOK_ERRORS -eq 0 ]]; then
        log_pass "All hook scripts validated"
    fi
else
    log_warn "hooks directory not found"
fi

# 6. Validate skill structure
echo ""
echo "--- Skill Structure Validation ---"

SKILL_STRUCTURE_ERRORS=0
while IFS= read -r -d '' skill_dir; do
    skill_name=$(basename "$skill_dir")

    # Check for SKILL.md
    if [[ ! -f "$skill_dir/SKILL.md" ]]; then
        log_fail "SKILL.md missing in $skill_name"
        ((SKILL_STRUCTURE_ERRORS++))
        continue
    fi

    # Check for frontmatter
    if ! grep -q "^---$" "$skill_dir/SKILL.md"; then
        log_warn "Missing frontmatter in $skill_name/SKILL.md"
    fi

done < <(find skills -maxdepth 1 -type d ! -path "skills" -print0 2>/dev/null || true)

if [[ $SKILL_STRUCTURE_ERRORS -eq 0 ]]; then
    log_pass "All skills have valid structure"
fi

# 7. Validate rulesync.jsonc in parent directory
echo ""
echo "--- RuleSync Configuration Validation ---"
cd /workspace

if [[ -f "rulesync.jsonc" ]]; then
    # Strip comments and validate JSON
    if jq empty rulesync.jsonc 2>/dev/null; then
        log_pass "rulesync.jsonc is valid JSON"

        # Check required fields
        if jq -e '.targets' rulesync.jsonc > /dev/null 2>&1; then
            TARGET_COUNT=$(jq '.targets | length' rulesync.jsonc)
            log_info "Found $TARGET_COUNT targets in rulesync.jsonc"

            # Verify all 7 targets are present
            EXPECTED_TARGETS=("claudecode" "opencode" "copilot" "geminicli" "codexcli" "antigravity" "factorydroid")
            MISSING_TARGETS=()

            for target in "${EXPECTED_TARGETS[@]}"; do
                if ! jq -e ".targets | index(\"$target\")" rulesync.jsonc > /dev/null 2>&1; then
                    MISSING_TARGETS+=("$target")
                fi
            done

            if [[ ${#MISSING_TARGETS[@]} -eq 0 ]]; then
                log_pass "All expected targets present in rulesync.jsonc"
            else
                log_fail "Missing targets in rulesync.jsonc: ${MISSING_TARGETS[*]}"
            fi
        else
            log_fail "Missing 'targets' field in rulesync.jsonc"
        fi

        if jq -e '.features' rulesync.jsonc > /dev/null 2>&1; then
            log_pass "features field present in rulesync.jsonc"
        else
            log_fail "Missing 'features' field in rulesync.jsonc"
        fi
    else
        log_fail "rulesync.jsonc contains invalid JSON"
    fi
else
    log_fail "rulesync.jsonc not found"
fi

# Summary
echo ""
echo "=========================================="
echo "Validation Summary"
echo "=========================================="
echo "Errors: $ERRORS"
echo "Warnings: $WARNINGS"
echo "=========================================="

# Exit with appropriate code
if [[ $ERRORS -gt 0 ]]; then
    exit 1
else
    log_pass "All validations passed"
    exit 0
fi
