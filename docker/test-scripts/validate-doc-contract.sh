#!/usr/bin/env bash
# Validate documentation contract for generated files

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

# Extract frontmatter from markdown file (between --- delimiters)
extract_frontmatter() {
    local file="$1"
    sed -n '2,/^---$/{ /^---$/d; p; }' "$file" 2>/dev/null || true
}

# Check if frontmatter contains a pattern
frontmatter_contains() {
    local file="$1"
    local pattern="$2"
    local fm
    fm=$(extract_frontmatter "$file")
    printf '%s' "$fm" | grep -qE "$pattern" 2>/dev/null
}

# Validate subagent file
validate_subagent() {
    local file="$1"
    local target="$2"
    local failed=0

    # Check file exists
    if [[ ! -f "$file" ]]; then
        log_error "Subagent file not found: $file"
        return 1
    fi

    # Check frontmatter exists
    if ! head -1 "$file" | grep -q '^---$'; then
        log_error "Missing frontmatter in: $file"
        return 1
    fi

    # Validate target-specific requirements
    case "$target" in
        claudecode)
            # Claude Code subagents should have allowed-tools or similar
            if ! frontmatter_contains "$file" "(allowed-tools|description|name):"; then
                log_warn "Claude subagent may be missing required fields: $file"
            fi
            ;;
        opencode)
            # OpenCode subagents should have agent blocks
            if ! frontmatter_contains "$file" "(agent:|name:|description):"; then
                log_warn "OpenCode subagent may be missing required fields: $file"
            fi
            ;;
        copilot)
            # Copilot subagents should have frontmatter with read/execute tools
            if ! frontmatter_contains "$file" "(read|execute|search):"; then
                log_warn "Copilot subagent may be missing tool definitions: $file"
            fi
            ;;
    esac

    return $failed
}

log_info "=========================================="
log_info "Documentation Contract Validation"
log_info "=========================================="
echo ""

FAILED=0

# Ensure RuleSync has been run
if [[ ! -d ".claude" && ! -d ".opencode" ]]; then
    log_info "Running RuleSync generate first..."
    rulesync install --frozen --silent 2>/dev/null || rulesync install
    rulesync generate --silent
fi

# Validate AGENTS.md
log_info "Validating AGENTS.md..."
if [[ -f "AGENTS.md" ]]; then
    # Rulesync generates AGENTS.md without frontmatter - content validation only
    if head -1 "AGENTS.md" | grep -q '^---$'; then
        log_success "AGENTS.md frontmatter OK"
    else
        log_info "AGENTS.md generated without frontmatter (expected for rulesync root files)"
    fi
    log_success "AGENTS.md exists and is valid"
else
    log_warn "AGENTS.md not found (may be expected for some configurations)"
fi

# Validate CLAUDE.md
log_info "Validating CLAUDE.md..."
if [[ -f "CLAUDE.md" ]]; then
    # Rulesync generates CLAUDE.md without frontmatter - content validation only
    if head -1 "CLAUDE.md" | grep -q '^---$'; then
        log_success "CLAUDE.md frontmatter OK"
    else
        log_info "CLAUDE.md generated without frontmatter (expected for rulesync root files)"
    fi
    log_success "CLAUDE.md exists and is valid"
else
    log_warn "CLAUDE.md not found (may be expected for some configurations)"
fi

# Validate subagents
log_info "Validating subagents..."

# Check Claude subagents
if [[ -d ".claude/agents" ]]; then
    for file in .claude/agents/*.md; do
        [[ -f "$file" ]] || continue
        if validate_subagent "$file" "claudecode"; then
            log_success "Claude subagent OK: $(basename "$file")"
        else
            FAILED=1
        fi
    done
fi

# Check OpenCode subagents
if [[ -d ".opencode/agent" ]]; then
    for file in .opencode/agent/*.md; do
        [[ -f "$file" ]] || continue
        if validate_subagent "$file" "opencode"; then
            log_success "OpenCode subagent OK: $(basename "$file")"
        else
            FAILED=1
        fi
    done
fi

# Check Copilot subagents
if [[ -d ".github/agents" ]]; then
    for file in .github/agents/*.md; do
        [[ -f "$file" ]] || continue
        if validate_subagent "$file" "copilot"; then
            log_success "Copilot subagent OK: $(basename "$file")"
        else
            FAILED=1
        fi
    done
fi

echo ""
if [[ $FAILED -eq 0 ]]; then
    log_success "Documentation contract validation PASSED"
else
    log_error "Documentation contract validation FAILED"
    exit 1
fi

log_info "=========================================="
exit 0
