#!/usr/bin/env bash
# Validate RuleSync generation for a specific target

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

# Get targets from environment or argument
TARGETS="${1:-${RULESYNC_TARGETS:-${TARGET:-}}}"
FEATURES="${RULESYNC_FEATURES:-*}"

if [[ -z "$TARGETS" ]]; then
    log_error "No target specified. Set TARGET or RULESYNC_TARGETS environment variable."
    exit 1
fi

log_info "Validating RuleSync generation..."
log_info "Targets: $TARGETS"
log_info "Features: $FEATURES"

# Install dependencies
log_info "Installing RuleSync dependencies..."
rulesync install --frozen --silent 2>/dev/null || rulesync install

# Clean previous generated outputs (but preserve .rulesync source)
log_info "Cleaning previous generated outputs..."
rm -rf \
    .agent \
    .claude \
    .codex \
    .factory \
    .gemini \
    .opencode \
    .vscode \
    .github/agents \
    .github/instructions \
    .github/prompts \
    .github/skills \
    .github/copilot-instructions.md \
    .mcp.json \
    AGENTS.md \
    CLAUDE.md \
    GEMINI.md \
    opencode.json 2>/dev/null || true

# Run RuleSync generate
log_info "Running rulesync generate --targets $TARGETS --features $FEATURES..."
if ! rulesync generate --targets "$TARGETS" --features "$FEATURES" --silent; then
    log_error "RuleSync generation failed"
    exit 1
fi

# Verify expected outputs based on target
log_info "Verifying generated outputs..."

validate_target_output() {
    local target="$1"
    local failed=0

    case "$target" in
        claudecode)
            [[ -f "CLAUDE.md" ]] || { log_error "Missing CLAUDE.md"; failed=1; }
            [[ -d ".claude/agents" ]] || { log_error "Missing .claude/agents"; failed=1; }
            [[ -d ".claude/skills" ]] || { log_warn "Missing .claude/skills (may be expected if no skills)"; }
            ;;
        opencode)
            # RuleSync generates .opencode/agent/ and other directories under .opencode/
            # It does NOT generate root AGENTS.md for opencode target
            [[ -d ".opencode/agent" ]] || { log_error "Missing .opencode/agent"; failed=1; }
            ;;
        copilot)
            [[ -f ".github/copilot-instructions.md" ]] || { log_error "Missing .github/copilot-instructions.md"; failed=1; }
            [[ -d ".github/agents" ]] || { log_error "Missing .github/agents"; failed=1; }
            ;;
        geminicli)
            [[ -f "GEMINI.md" ]] || { log_error "Missing GEMINI.md"; failed=1; }
            [[ -d ".gemini/skills" ]] || { log_warn "Missing .gemini/skills (may be expected)"; }
            ;;
        codexcli)
            [[ -d ".codex/agents" ]] || { log_error "Missing .codex/agents"; failed=1; }
            ;;
        factorydroid)
            # RuleSync generates .factory/ directory for factorydroid target
            # Contains: mcp.json, settings.json, and rules/ subdirectory
            [[ -d ".factory" ]] || { log_error "Missing .factory directory"; failed=1; }
            [[ -f ".factory/mcp.json" ]] || { log_error "Missing .factory/mcp.json"; failed=1; }
            ;;
        antigravity)
            [[ -d ".gemini/antigravity/skills" ]] || { log_warn "Missing .gemini/antigravity/skills (may be expected)"; }
            ;;
    esac

    return $failed
}

# Validate outputs for each target
IFS=',' read -ra TARGET_ARRAY <<< "$TARGETS"
validation_failed=0

for target in "${TARGET_ARRAY[@]}"; do
    target=$(echo "$target" | tr -d ' ')
    log_info "Validating outputs for target: $target"
    if ! validate_target_output "$target"; then
        validation_failed=1
    fi
done

if [[ $validation_failed -eq 1 ]]; then
    log_error "Target validation failed"
    exit 1
fi

log_success "All targets validated successfully!"
log_info "Targets: $TARGETS"
log_info "Features: $FEATURES"
exit 0
