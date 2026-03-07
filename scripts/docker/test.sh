#!/usr/bin/env bash
# Master test script for RuleSync-first testing infrastructure
# Validates RuleSync JSON files, runs generation tests, and outputs JSON results

set -euo pipefail

# Early exit: ensure we're in the right environment
if [[ ! -d "/workspace/.rulesync" ]]; then
    echo "ERROR: .rulesync directory not found at /workspace" >&2
    exit 1
fi

# Parse arguments
OUTPUT_DIR="${TEST_OUTPUT_DIR:-/test-results}"
VERBOSE=false
TARGET=""

while [[ $# -gt 0 ]]; do
    case $1 in
        --output)
            OUTPUT_DIR="$2"
            shift 2
            ;;
        --verbose)
            VERBOSE=true
            shift
            ;;
        --target)
            TARGET="$2"
            shift 2
            ;;
        *)
            echo "Unknown option: $1" >&2
            exit 1
            ;;
    esac
done

# Create output directory
mkdir -p "$OUTPUT_DIR"

# Log RuleSync version at startup
echo "=== RuleSync Version ==="
rulesync --version
RULESYNC_VERSION=$(rulesync --version 2>/dev/null | head -1 || echo "unknown")

# Initialize test results
declare -A TEST_RESULTS
declare -A TEST_DURATIONS
OVERALL_START=$(date +%s.%N)

# Log helper
log() {
    local level="$1"
    shift
    echo "[$level] $*"
}

# Run a test and capture results
run_test() {
    local test_name="$1"
    local test_cmd="$2"

    log "INFO" "Running test: $test_name"
    local start_time=$(date +%s.%N)

    if eval "$test_cmd" >"$OUTPUT_DIR/${test_name}.log" 2>&1; then
        local end_time=$(date +%s.%N)
        local duration=$(echo "$end_time - $start_time" | bc)
        TEST_RESULTS[$test_name]="PASSED"
        TEST_DURATIONS[$test_name]=$duration
        log "PASS" "$test_name (${duration}s)"
        return 0
    else
        local end_time=$(date +%s.%N)
        local duration=$(echo "$end_time - $start_time" | bc)
        TEST_RESULTS[$test_name]="FAILED"
        TEST_DURATIONS[$test_name]=$duration
        log "FAIL" "$test_name (${duration}s)"
        if [[ "$VERBOSE" == true ]]; then
            cat "$OUTPUT_DIR/${test_name}.log"
        fi
        return 1
    fi
}

# Test 1: Validate RuleSync JSON files
validate_json_files() {
    local errors=0

    # Validate rulesync.jsonc
    if [[ -f "/workspace/rulesync.jsonc" ]]; then
        if ! jq empty "/workspace/rulesync.jsonc" 2>/dev/null; then
            log "ERROR" "Invalid JSON in rulesync.jsonc"
            ((errors++))
        fi
    fi

    # Validate hooks.json
    if [[ -f "/workspace/.rulesync/hooks.json" ]]; then
        if ! jq empty "/workspace/.rulesync/hooks.json" 2>/dev/null; then
            log "ERROR" "Invalid JSON in hooks.json"
            ((errors++))
        fi
    fi

    # Validate mcp.json
    if [[ -f "/workspace/.rulesync/mcp.json" ]]; then
        if ! jq empty "/workspace/.rulesync/mcp.json" 2>/dev/null; then
            log "ERROR" "Invalid JSON in mcp.json"
            ((errors++))
        fi
    fi

    # Validate all skill SKILL.md frontmatter
    find "/workspace/.rulesync/skills" -name "SKILL.md" -type f | while read -r skill_file; do
        if ! grep -q "^---$" "$skill_file"; then
            log "WARN" "Missing frontmatter in $skill_file"
        fi
    done

    return $errors
}

# Test 2: Run generation tests
test_generation() {
    local test_target="${1:-all}"
    local tmpdir
    tmpdir=$(mktemp -d)
    trap "rm -rf $tmpdir" RETURN

    cd /workspace

    if [[ "$test_target" == "all" ]]; then
        # Test all targets
        if ! rulesync generate --check --silent 2>/dev/null; then
            log "ERROR" "RuleSync generation check failed"
            return 1
        fi
    else
        # Test specific target
        if ! rulesync generate --targets "$test_target" --output "$tmpdir" --silent 2>/dev/null; then
            log "ERROR" "RuleSync generation failed for target: $test_target"
            return 1
        fi
    fi

    return 0
}

# Test 3: Run target-specific tests
test_target_specific() {
    local target="$1"

    case "$target" in
        claudecode)
            test_claudecode
            ;;
        opencode)
            test_opencode
            ;;
        copilot)
            test_copilot
            ;;
        geminicli)
            test_geminicli
            ;;
        codexcli)
            test_codexcli
            ;;
        antigravity)
            test_antigravity
            ;;
        factorydroid)
            test_factorydroid
            ;;
        *)
            log "ERROR" "Unknown target: $target"
            return 1
            ;;
    esac
}

# Individual target tests
test_claudecode() {
    local tmpdir
    tmpdir=$(mktemp -d)
    trap "rm -rf $tmpdir" RETURN

    cd /workspace
    rulesync generate --targets claudecode --output "$tmpdir" --silent

    # Check expected outputs
    [[ -f "$tmpdir/.claude/agents/dotnet-architect.md" ]] || return 1
    [[ -f "$tmpdir/CLAUDE.md" ]] || return 1

    return 0
}

test_opencode() {
    local tmpdir
    tmpdir=$(mktemp -d)
    trap "rm -rf $tmpdir" RETURN

    cd /workspace
    rulesync generate --targets opencode --output "$tmpdir" --silent

    # Check expected outputs
    [[ -f "$tmpdir/.opencode/agent/dotnet-architect.md" ]] || return 1
    [[ -f "$tmpdir/AGENTS.md" ]] || return 1

    return 0
}

test_copilot() {
    local tmpdir
    tmpdir=$(mktemp -d)
    trap "rm -rf $tmpdir" RETURN

    cd /workspace
    rulesync generate --targets copilot --output "$tmpdir" --silent

    # Check expected outputs
    [[ -f "$tmpdir/.github/agents/dotnet-architect.md" ]] || return 1
    [[ -f "$tmpdir/.github/copilot-instructions.md" ]] || return 1

    return 0
}

test_geminicli() {
    local tmpdir
    tmpdir=$(mktemp -d)
    trap "rm -rf $tmpdir" RETURN

    cd /workspace
    rulesync generate --targets geminicli --output "$tmpdir" --silent

    # Check expected outputs
    [[ -f "$tmpdir/GEMINI.md" ]] || return 1
    [[ -d "$tmpdir/.gemini/skills" ]] || return 1

    return 0
}

test_codexcli() {
    local tmpdir
    tmpdir=$(mktemp -d)
    trap "rm -rf $tmpdir" RETURN

    cd /workspace
    rulesync generate --targets codexcli --output "$tmpdir" --silent

    # Check expected outputs
    [[ -d "$tmpdir/.codex/agents" ]] || return 1

    return 0
}

test_antigravity() {
    local tmpdir
    tmpdir=$(mktemp -d)
    trap "rm -rf $tmpdir" RETURN

    cd /workspace
    rulesync generate --targets antigravity --output "$tmpdir" --silent

    # Check expected outputs
    [[ -d "$tmpdir/.gemini/antigravity/skills" ]] || return 1

    return 0
}

test_factorydroid() {
    local tmpdir
    tmpdir=$(mktemp -d)
    trap "rm -rf $tmpdir" RETURN

    cd /workspace
    rulesync generate --targets factorydroid --output "$tmpdir" --silent

    # Check expected outputs
    [[ -f "$tmpdir/.agent/skills" ]] || [[ -d "$tmpdir/.agent/skills" ]] || return 1

    return 0
}

# Main test execution
echo "=========================================="
echo "RuleSync Testing Infrastructure"
echo "RuleSync Version: $RULESYNC_VERSION"
echo "Output Directory: $OUTPUT_DIR"
echo "=========================================="

# Run validation tests
run_test "json-validation" "validate_json_files"

# Run generation tests
if [[ -n "$TARGET" ]]; then
    run_test "generation-$TARGET" "test_generation $TARGET"
    run_test "target-$TARGET" "test_target_specific $TARGET"
else
    run_test "generation-all" "test_generation all"

    # Run target-specific tests for all targets
    for tgt in claudecode opencode copilot geminicli codexcli antigravity factorydroid; do
        run_test "target-$tgt" "test_target_specific $tgt"
    done
fi

# Run validation script
run_test "validation-all" "/workspace/scripts/docker/validate-all.sh"

# Calculate overall results
OVERALL_END=$(date +%s.%N)
OVERALL_DURATION=$(echo "$OVERALL_END - $OVERALL_START" | bc)

PASSED=0
FAILED=0

for result in "${TEST_RESULTS[@]}"; do
    if [[ "$result" == "PASSED" ]]; then
        ((PASSED++))
    else
        ((FAILED++))
    fi
done

TOTAL=$((PASSED + FAILED))

# Output JSON results
cat > "$OUTPUT_DIR/results.json" << EOF
{
  "testRun": {
    "startTime": $(date -d "@$(echo $OVERALL_START | cut -d. -f1)" -u +%Y-%m-%dT%H:%M:%SZ),
    "duration": $OVERALL_DURATION,
    "rulesyncVersion": "$RULESYNC_VERSION"
  },
  "summary": {
    "total": $TOTAL,
    "passed": $PASSED,
    "failed": $FAILED,
    "successRate": $(echo "scale=2; $PASSED * 100 / $TOTAL" | bc)
  },
  "tests": [
EOF

# Add individual test results
first=true
for test_name in "${!TEST_RESULTS[@]}"; do
    if [[ "$first" == true ]]; then
        first=false
    else
        echo "," >> "$OUTPUT_DIR/results.json"
    fi

    status="${TEST_RESULTS[$test_name]}"
    duration="${TEST_DURATIONS[$test_name]}"

    cat >> "$OUTPUT_DIR/results.json" << EOF
    {
      "name": "$test_name",
      "status": "$status",
      "duration": $duration
    }
EOF
done

cat >> "$OUTPUT_DIR/results.json" << EOF

  ]
}
EOF

# Print summary
echo ""
echo "=========================================="
echo "Test Summary"
echo "=========================================="
echo "Total: $TOTAL"
echo "Passed: $PASSED"
echo "Failed: $FAILED"
echo "Duration: ${OVERALL_DURATION}s"
echo "Success Rate: $(echo "scale=1; $PASSED * 100 / $TOTAL" | bc)%"
echo "=========================================="
echo "Results saved to: $OUTPUT_DIR/results.json"

# Exit with appropriate code
if [[ $FAILED -gt 0 ]]; then
    exit 1
else
    exit 0
fi
