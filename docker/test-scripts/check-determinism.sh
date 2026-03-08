#!/usr/bin/env bash
# Check RuleSync generation determinism
# Runs generation twice and verifies outputs are identical

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

# Paths to checksum for determinism verification
CHECKSUM_PATHS=(
    ".agent"
    ".claude"
    ".codex"
    ".gemini"
    ".geminiignore"
    ".opencode"
    ".vscode"
    ".github/agents"
    ".github/instructions"
    ".github/prompts"
    ".github/skills"
    ".github/copilot-instructions.md"
    ".mcp.json"
    "AGENTS.md"
    "CLAUDE.md"
    "GEMINI.md"
    "opencode.json"
)

# Create a deterministic checksum of paths
checksum_paths() {
    local -a existing=()
    local rel
    for rel in "${CHECKSUM_PATHS[@]}"; do
        if [[ -e "$rel" ]]; then
            existing+=("$rel")
        fi
    done

    if [[ ${#existing[@]} -eq 0 ]]; then
        printf 'EMPTY\n'
        return
    fi

    # Use tar with deterministic options for reproducible checksum
    tar --sort=name \
        --mtime='UTC 1970-01-01' \
        --owner=0 \
        --group=0 \
        --numeric-owner \
        -cf - \
        "${existing[@]}" 2>/dev/null | \
        sha256sum | \
        cut -d' ' -f1
}

log_info "=========================================="
log_info "RuleSync Determinism Check"
log_info "=========================================="
echo ""

# Create temporary directory for clean workspace
TMP_ROOT=$(mktemp -d)
WORK_DIR="$TMP_ROOT/work"
mkdir -p "$WORK_DIR"

cleanup() {
    rm -rf "$TMP_ROOT"
}
trap cleanup EXIT

log_info "Copying source to isolated workspace..."
# Copy source excluding git and node_modules
tar -cf - \
    --exclude ".git" \
    --exclude "node_modules" \
    --exclude "plugins" \
    --exclude ".agent" \
    --exclude ".claude" \
    --exclude ".codex" \
    --exclude ".gemini" \
    --exclude ".opencode" \
    --exclude ".vscode" \
    -C . . 2>/dev/null | \
    tar -xf - -C "$WORK_DIR" 2>/dev/null || true

cd "$WORK_DIR"

# Install RuleSync
log_info "Installing RuleSync..."
rulesync install --frozen --silent 2>/dev/null || rulesync install

# First generation
log_info "Running first generation..."
rulesync generate --targets "$ALL_TARGETS" --silent

PROJECT_CHECKSUM_BEFORE=$(checksum_paths)
log_info "First generation checksum: ${PROJECT_CHECKSUM_BEFORE:0:16}..."

# Second generation (should produce identical output)
log_info "Running second generation..."
rulesync generate --targets "$ALL_TARGETS" --silent

PROJECT_CHECKSUM_AFTER=$(checksum_paths)
log_info "Second generation checksum: ${PROJECT_CHECKSUM_AFTER:0:16}..."

# Compare checksums
echo ""
if [[ "$PROJECT_CHECKSUM_BEFORE" == "$PROJECT_CHECKSUM_AFTER" ]]; then
    log_success "Determinism check PASSED"
    log_info "Both generations produced identical output"
else
    log_error "Determinism check FAILED"
    log_error "Checksums differ between consecutive runs"
    log_error "Before: $PROJECT_CHECKSUM_BEFORE"
    log_error "After:  $PROJECT_CHECKSUM_AFTER"
    exit 1
fi

log_info "=========================================="
exit 0
