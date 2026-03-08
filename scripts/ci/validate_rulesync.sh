#!/usr/bin/env bash
# shellcheck disable=SC2154,SC2250

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=../lib/common.sh
source "$SCRIPT_DIR/../lib/common.sh"

TMP_ROOT="$(mktemp -d)"
WORK_DIR="$TMP_ROOT/work"
GLOBAL_HOME="$TMP_ROOT/home"

cleanup() {
  rm -rf "$TMP_ROOT"
}
trap cleanup EXIT

reset_generated_workspace() {
  local target_root="$1"

  rm -rf \
    "$target_root/.agent" \
    "$target_root/.claude" \
    "$target_root/.codex" \
    "$target_root/.gemini" \
    "$target_root/.opencode" \
    "$target_root/.vscode" \
    "$target_root/.github/agents" \
    "$target_root/.github/instructions" \
    "$target_root/.github/prompts" \
    "$target_root/.github/skills"

  rm -f \
    "$target_root/.github/copilot-instructions.md" \
    "$target_root/.mcp.json" \
    "$target_root/AGENTS.md" \
    "$target_root/CLAUDE.md" \
    "$target_root/GEMINI.md" \
    "$target_root/opencode.json"
}

require_path() {
  local path="$1"
  if [[ ! -e "$path" ]]; then
    fail "Expected generated path missing: $path"
  fi
}

# Paths to checksum for determinism verification (defined once, used twice)
CHECKSUM_PATHS=(
  .agent
  .claude
  .codex
  .gemini
  .geminiignore
  .opencode
  .vscode
  .github/agents
  .github/instructions
  .github/prompts
  .github/skills
  .github/copilot-instructions.md
  .mcp.json
  AGENTS.md
  CLAUDE.md
  GEMINI.md
  opencode.json
)

checksum_paths() {
  local root="$1"
  shift

  local -a existing=()
  local rel
  for rel in "$@"; do
    if [[ -e "$root/$rel" ]]; then
      existing+=("$rel")
    fi
  done

  if [[ ${#existing[@]} -eq 0 ]]; then
    printf 'EMPTY\n'
    return
  fi

  (
    cd "$root"
    tar --sort=name --mtime='UTC 1970-01-01' --owner=0 --group=0 --numeric-owner -cf - "${existing[@]}" | sha256sum | cut -d' ' -f1
  )
}

mkdir -p "$WORK_DIR" "$GLOBAL_HOME"

log "Creating isolated workspace for RuleSync validation"
mkdir -p "$WORK_DIR"
tar -cf - \
  --exclude ".git" \
  --exclude "node_modules" \
  --exclude "plugins" \
  -C "$REPO_ROOT" . | tar -xf - -C "$WORK_DIR"

reset_generated_workspace "$WORK_DIR"

pushd "$WORK_DIR" >/dev/null
log "Running rulesync install"
run_rulesync install --frozen --silent || run_rulesync install

log "Running rulesync generate"
run_rulesync generate --silent

project_checksum_before="$(checksum_paths "$WORK_DIR" "${CHECKSUM_PATHS[@]}")"

log "Running rulesync generate again for determinism check"
run_rulesync generate --silent

project_checksum_after="$(checksum_paths "$WORK_DIR" "${CHECKSUM_PATHS[@]}")"

if [[ "$project_checksum_before" != "$project_checksum_after" ]]; then
  fail "RuleSync project generation is not deterministic across consecutive runs"
fi

log "Verifying generated target output paths"
require_path "$WORK_DIR/.claude/agents"
require_path "$WORK_DIR/.github/instructions"
require_path "$WORK_DIR/.github/agents"
require_path "$WORK_DIR/.gemini/skills"
require_path "$WORK_DIR/.opencode/agent"
require_path "$WORK_DIR/AGENTS.md"
require_path "$WORK_DIR/.codex/agents"
require_path "$WORK_DIR/.agent/rules"
require_path "$WORK_DIR/.agent/workflows"
require_path "$WORK_DIR/.agent/skills"

log "Verifying generated subagent content"

require_content() {
  local file="$1" pattern="$2" desc="$3"
  if ! grep -qE "$pattern" "$file" 2>/dev/null; then
    fail "Generated file $(basename "$file") missing expected content: $desc"
  fi
}

# Extract frontmatter only (between --- delimiters) from a markdown file
extract_fm() {
  sed -n '2,/^---$/{ /^---$/d; p; }' "$1"
}

# Check frontmatter does NOT contain a pattern
reject_in_frontmatter() {
  local file="$1" pattern="$2" desc="$3"
  local fm
  fm="$(extract_fm "$file")"
  if printf '%s' "$fm" | grep -qE "$pattern" 2>/dev/null; then
    fail "Generated file $(basename "$file") frontmatter should not contain: $desc"
  fi
}

# Check frontmatter DOES contain a pattern
require_in_frontmatter() {
  local file="$1" pattern="$2" desc="$3"
  local fm
  fm="$(extract_fm "$file")"
  if ! printf '%s' "$fm" | grep -qE "$pattern" 2>/dev/null; then
    fail "Generated file $(basename "$file") frontmatter missing: $desc"
  fi
}

# --- Read-only agent: security-reviewer ---
require_in_frontmatter "$WORK_DIR/.claude/agents/dotnet-security-reviewer.md" \
  "^allowed-tools:" "Claude Code allowed-tools field"
require_in_frontmatter "$WORK_DIR/.claude/agents/dotnet-security-reviewer.md" \
  "Read" "Claude Code Read tool"
reject_in_frontmatter "$WORK_DIR/.claude/agents/dotnet-security-reviewer.md" \
  "Bash" "Bash (read-only profile)"

require_content "$WORK_DIR/.opencode/agent/dotnet-security-reviewer.md" \
  "bash: false" "OpenCode bash: false"
require_content "$WORK_DIR/.opencode/agent/dotnet-security-reviewer.md" \
  "edit: false" "OpenCode edit: false"
require_content "$WORK_DIR/.opencode/agent/dotnet-security-reviewer.md" \
  "write: false" "OpenCode write: false"

require_in_frontmatter "$WORK_DIR/.github/agents/dotnet-security-reviewer.md" \
  "read" "Copilot read tool"
require_in_frontmatter "$WORK_DIR/.github/agents/dotnet-security-reviewer.md" \
  "search" "Copilot search tool"
reject_in_frontmatter "$WORK_DIR/.github/agents/dotnet-security-reviewer.md" \
  "execute" "execute (read-only profile)"

require_content "$WORK_DIR/.codex/agents/dotnet-security-reviewer.toml" \
  'sandbox_mode = "read-only"' "Codex CLI sandbox_mode read-only"

# --- Standard agent across Claude/OpenCode/Copilot; read-only Codex profile: architect ---
require_in_frontmatter "$WORK_DIR/.claude/agents/dotnet-architect.md" \
  "Bash" "Claude Code Bash tool for standard agent"
reject_in_frontmatter "$WORK_DIR/.claude/agents/dotnet-architect.md" \
  "^\s+- Edit$" "Edit (standard profile)"
reject_in_frontmatter "$WORK_DIR/.claude/agents/dotnet-architect.md" \
  "^\s+- Write$" "Write (standard profile)"

require_content "$WORK_DIR/.opencode/agent/dotnet-architect.md" \
  "bash: true" "OpenCode bash: true for standard agent"
require_content "$WORK_DIR/.opencode/agent/dotnet-architect.md" \
  "edit: false" "OpenCode edit: false for standard agent"

require_in_frontmatter "$WORK_DIR/.github/agents/dotnet-architect.md" \
  "execute" "Copilot execute tool for standard agent"

require_content "$WORK_DIR/.codex/agents/dotnet-architect.toml" \
  'sandbox_mode = "inherit"' "Codex CLI sandbox_mode inherit for architect"

# --- Full agent: docs-generator ---
require_in_frontmatter "$WORK_DIR/.claude/agents/dotnet-docs-generator.md" \
  "Edit" "Claude Code Edit tool for full agent"
require_in_frontmatter "$WORK_DIR/.claude/agents/dotnet-docs-generator.md" \
  "Write" "Claude Code Write tool for full agent"

require_content "$WORK_DIR/.opencode/agent/dotnet-docs-generator.md" \
  "bash: true" "OpenCode bash: true for full agent"
require_content "$WORK_DIR/.opencode/agent/dotnet-docs-generator.md" \
  "edit: true" "OpenCode edit: true for full agent"
require_content "$WORK_DIR/.opencode/agent/dotnet-docs-generator.md" \
  "write: true" "OpenCode write: true for full agent"

require_in_frontmatter "$WORK_DIR/.github/agents/dotnet-docs-generator.md" \
  "edit" "Copilot edit tool for full agent"

log "Generated subagent content checks passed"

log "Running global codex command generation"
HOME="$GLOBAL_HOME" run_rulesync generate --targets codexcli --features commands --global --silent
require_path "$GLOBAL_HOME/.codex/prompts"

codex_checksum_before="$(checksum_paths "$GLOBAL_HOME" .codex/prompts)"
HOME="$GLOBAL_HOME" run_rulesync generate --targets codexcli --features commands --global --silent
codex_checksum_after="$(checksum_paths "$GLOBAL_HOME" .codex/prompts)"

if [[ "$codex_checksum_before" != "$codex_checksum_after" ]]; then
  fail "RuleSync global codex generation is not deterministic across consecutive runs"
fi

log "Running global antigravity skill generation"
HOME="$GLOBAL_HOME" run_rulesync generate --targets antigravity --features skills --global --silent

antigravity_root="$GLOBAL_HOME"
if [[ ! -d "$antigravity_root/.gemini/antigravity/skills" && -d "$WORK_DIR/.gemini/antigravity/skills" ]]; then
  antigravity_root="$WORK_DIR"
fi

require_path "$antigravity_root/.gemini/antigravity/skills"

antigravity_checksum_before="$(checksum_paths "$antigravity_root" .gemini/antigravity/skills)"
HOME="$GLOBAL_HOME" run_rulesync generate --targets antigravity --features skills --global --silent
antigravity_checksum_after="$(checksum_paths "$antigravity_root" .gemini/antigravity/skills)"

if [[ "$antigravity_checksum_before" != "$antigravity_checksum_after" ]]; then
  fail "RuleSync global antigravity generation is not deterministic across consecutive runs"
fi
popd >/dev/null

log "RuleSync validation completed successfully"
