#!/usr/bin/env bash
# Validate RuleSync generation for all targets

set -euo pipefail

# Color codes
readonly RED='\033[0;31m'
readonly GREEN='\033[0;32m'
readonly YELLOW='\033[1;33m'
readonly BLUE='\033[0;34m'
readonly NC='\033[0m'

log_info() { echo -e "${BLUE}[INFO]${NC} $*"; }
log_success() { echo -e "${GREEN}[SUCCESS]${NC} $*"; }
log_warn() { echo -e "${YELLOW}[WARN]${NC} $*"; }
log_error() { echo -e "${RED}[ERROR]${NC} $*"; }

# All supported targets
ALL_TARGETS="claudecode,opencode,copilot,geminicli,codexcli,factorydroid,antigravity"

log_info "=========================================="
log_info "RuleSync Validation - All Targets"
log_info "=========================================="
echo ""

# Track failures
FAILED=0
FAILED_TARGETS=()

# Step 1: Validate generation for all targets
log_info "Step 1/4: Validating generation for all targets..."
echo ""

if ! /usr/local/bin/validate-target "$ALL_TARGETS"; then
    log_error "Generation validation failed"
    FAILED=1
fi

# Step 2: Run determinism check
log_info "Step 2/4: Running determinism check..."
echo ""

if ! /usr/local/bin/check-determinism; then
    log_error "Determinism check failed"
    FAILED=1
fi

# Step 3: Validate documentation contract
log_info "Step 3/4: Validating documentation contract..."
echo ""

if ! /usr/local/bin/validate-doc-contract; then
    log_error "Documentation contract validation failed"
    FAILED=1
fi

# Step 4: Validate subagent content (if scripts available)
log_info "Step 4/4: Running additional checks..."
echo ""

# Check if validation scripts exist in workspace
if [[ -f "scripts/ci/validate_subagents.sh" ]]; then
    log_info "Running subagent validation..."
    if ! bash scripts/ci/validate_subagents.sh; then
        log_error "Subagent validation failed"
        FAILED=1
    fi
fi

# Summary
echo ""
log_info "=========================================="
if [[ $FAILED -eq 0 ]]; then
    log_success "All validations passed!"
    log_info "Targets validated: $ALL_TARGETS"
else
    log_error "Some validations failed!"
    exit 1
fi
log_info "=========================================="

exit 0
