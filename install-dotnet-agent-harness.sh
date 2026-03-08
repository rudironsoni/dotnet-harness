#!/bin/bash
#
# install-dotnet-agent-harness.sh
# One-step installer for dotnet-agent-harness toolkit
#
# This script installs the .rulesync configuration and downloads all required
# hook scripts that rulesync doesn't automatically fetch.
#
# Usage:
#   ./install-dotnet-agent-harness.sh [OPTIONS]
#
# Options:
#   --source <repo>     Source GitHub repo (default: rudironsoni/dotnet-agent-harness)
#   --targets <list>    Comma-separated target platforms
#                       (default: claudecode,copilot,opencode,geminicli,factorydroid,codexcli,antigravity)
#   --path <dir>        Installation directory (default: current directory)
#   --help, -h          Show this help message
#
# Examples:
#   ./install-dotnet-agent-harness.sh
#   ./install-dotnet-agent-harness.sh --targets claudecode,copilot
#   ./install-dotnet-agent-harness.sh --source myfork/dotnet-agent-harness --path ./my-project
#

set -euo pipefail

# =============================================================================
# Configuration
# =============================================================================

# Default values
DEFAULT_SOURCE="rudironsoni/dotnet-agent-harness"
DEFAULT_TARGETS="claudecode,copilot,opencode,geminicli,factorydroid,codexcli,antigravity"
DEFAULT_PATH="."

# Script metadata
SCRIPT_NAME="$(basename "$0")"
SCRIPT_VERSION="1.0.0"

# Hook scripts to download (based on hooks.json references)
HOOK_SCRIPTS=(
    "dotnet-agent-harness-session-start.sh"
    "dotnet-agent-harness-post-edit-roslyn.sh"
    "dotnet-agent-harness-slopwatch.sh"
    "dotnet-agent-harness-inline-error-recovery.sh"
    "dotnet-agent-harness-error-recovery.sh"
)

# =============================================================================
# Color Output
# =============================================================================

if [[ -t 1 ]]; then
    # Terminal supports colors
    # Use $'...' syntax to properly interpret escape sequences
    COLOR_RESET=$'\033[0m'
    COLOR_RED=$'\033[0;31m'
    COLOR_GREEN=$'\033[0;32m'
    COLOR_YELLOW=$'\033[1;33m'
    COLOR_BLUE=$'\033[0;34m'
    COLOR_CYAN=$'\033[0;36m'
    COLOR_BOLD=$'\033[1m'
else
    # No color support
    COLOR_RESET=''
    COLOR_RED=''
    COLOR_GREEN=''
    COLOR_YELLOW=''
    COLOR_BLUE=''
    COLOR_CYAN=''
    COLOR_BOLD=''
fi

# =============================================================================
# Logging Functions
# =============================================================================

log_info() {
    printf "${COLOR_BLUE}[INFO]${COLOR_RESET} %s\n" "$1"
}

log_success() {
    printf "${COLOR_GREEN}[OK]${COLOR_RESET} %s\n" "$1"
}

log_warning() {
    printf "${COLOR_YELLOW}[WARN]${COLOR_RESET} %s\n" "$1" >&2
}

log_error() {
    printf "${COLOR_RED}[ERROR]${COLOR_RESET} %s\n" "$1" >&2
}

log_step() {
    printf "\n${COLOR_CYAN}==>${COLOR_RESET} ${COLOR_BOLD}%s${COLOR_RESET}\n" "$1"
}

# =============================================================================
# Helper Functions
# =============================================================================

show_usage() {
    cat << EOF
${COLOR_BOLD}dotnet-agent-harness Installer v${SCRIPT_VERSION}${COLOR_RESET}

Installs the dotnet-agent-harness toolkit into your project.

${COLOR_BOLD}Usage:${COLOR_RESET}
  ./${SCRIPT_NAME} [OPTIONS]

${COLOR_BOLD}Options:${COLOR_RESET}
  --source <repo>     Source GitHub repository
                      (default: ${DEFAULT_SOURCE})
  --targets <list>    Comma-separated list of target platforms
                      (default: ${DEFAULT_TARGETS})
  --path <dir>        Directory to install into
                      (default: ${DEFAULT_PATH})
  --help, -h          Show this help message

${COLOR_BOLD}Supported Targets:${COLOR_RESET}
  claudecode    - Claude Code integration
  copilot       - GitHub Copilot CLI integration
  opencode      - OpenCode integration
  geminicli     - Gemini CLI integration
  factorydroid  - Factory Droid integration
  codexcli      - Codex CLI integration
  antigravity   - Antigravity integration

${COLOR_BOLD}Examples:${COLOR_RESET}
  # Install with defaults
  ./${SCRIPT_NAME}

  # Install only specific targets
  ./${SCRIPT_NAME} --targets claudecode,copilot

  # Install from a fork
  ./${SCRIPT_NAME} --source myuser/dotnet-agent-harness --path ./my-project

${COLOR_BOLD}Notes:${COLOR_RESET}
  - Requires 'rulesync' to be installed and in PATH
  - Requires 'curl' for downloading hook scripts
  - Hook scripts are downloaded to .rulesync/hooks/

EOF
}

command_exists() {
    command -v "$1" >/dev/null 2>&1
}

validate_source() {
    local source="$1"
    # Basic validation: must contain exactly one slash
    if [[ ! "${source}" =~ ^[^/]+/[^/]+$ ]]; then
        log_error "Invalid source format: ${source}"
        log_error "Expected format: owner/repo"
        exit 1
    fi
}

validate_path() {
    local path="$1"
    # Check if path is writable
    if [[ ! -d "${path}" ]]; then
        log_info "Creating directory: $path"
        if ! mkdir -p "${path}"; then
            log_error "Failed to create directory: $path"
            exit 1
        fi
    fi
    if [[ ! -w "${path}" ]]; then
        log_error "Directory is not writable: $path"
        exit 1
    fi
}

download_hook_script() {
    local script_name="$1"
    local source="$2"
    local hooks_dir="$3"
    local url="https://raw.githubusercontent.com/${source}/main/.rulesync/hooks/${script_name}"
    local output_path="${hooks_dir}/${script_name}"
    local temp_file
    local http_code

    temp_file=$(mktemp)
    trap "rm -f '${temp_file}'" RETURN

    log_info "Downloading ${script_name}..."

    # Download with curl, capture HTTP status code
    http_code=$(curl -sL -w "%{http_code}" -o "${temp_file}" "${url}" 2>/dev/null)

    if [[ "$http_code" -ne 200 ]]; then
        log_error "Failed to download ${script_name} (HTTP ${http_code})"
        log_error "URL: ${url}"
        return 1
    fi

    # Validate it's a shell script
    if [[ ! -s "${temp_file}" ]]; then
        log_error "Downloaded file is empty: ${script_name}"
        return 1
    fi

    if ! head -1 "${temp_file}" | grep -qE '^#!.*(bash|sh)'; then
        log_warning "Downloaded file doesn't look like a shell script: ${script_name}"
    fi

    # Move to final location
    if ! mv "${temp_file}" "$output_path"; then
        log_error "Failed to write: ${output_path}"
        return 1
    fi

    log_success "Downloaded ${script_name}"
    return 0
}

make_executable() {
    local file="$1"
    if [[ -f "$file" ]]; then
        chmod +x "$file"
        log_success "Made executable: $(basename "$file")"
    fi
}

# =============================================================================
# Main Functions
# =============================================================================

parse_args() {
    SOURCE="$DEFAULT_SOURCE"
    TARGETS="$DEFAULT_TARGETS"
    INSTALL_PATH="$DEFAULT_PATH"

    while [[ $# -gt 0 ]]; do
        case "$1" in
            --source)
                if [[ -z "${2:-}" ]]; then
                    log_error "--source requires a value"
                    exit 1
                fi
                SOURCE="$2"
                shift 2
                ;;
            --targets)
                if [[ -z "${2:-}" ]]; then
                    log_error "--targets requires a value"
                    exit 1
                fi
                TARGETS="$2"
                shift 2
                ;;
            --path)
                if [[ -z "${2:-}" ]]; then
                    log_error "--path requires a value"
                    exit 1
                fi
                INSTALL_PATH="$2"
                shift 2
                ;;
            --help|-h)
                show_usage
                exit 0
                ;;
            *)
                log_error "Unknown option: $1"
                log_error "Use --help for usage information"
                exit 1
                ;;
        esac
    done
}

check_prerequisites() {
    log_step "Checking prerequisites"

    # Check for rulesync
    if ! command_exists rulesync; then
        log_error "rulesync command not found in PATH"
        log_error ""
        log_error "Please install rulesync first:"
        log_error "  npm install -g @codewyre/rulesync"
        log_error "  # or"
        log_error "  yarn global add @codewyre/rulesync"
        log_error "  # or"
        log_error "  pnpm add -g @codewyre/rulesync"
        exit 1
    fi
    log_success "Found rulesync: $(command -v rulesync)"

    # Check for curl
    if ! command_exists curl; then
        log_error "curl command not found in PATH"
        log_error ""
        log_error "Please install curl to download hook scripts."
        exit 1
    fi
    log_success "Found curl: $(command -v curl)"

    # Validate source format
    validate_source "$SOURCE"

    # Validate installation path
    validate_path "$INSTALL_PATH"

    log_success "All prerequisites met"
}

run_rulesync_fetch() {
    log_step "Running rulesync fetch"
    log_info "Source: ${COLOR_CYAN}${SOURCE}${COLOR_RESET}"
    log_info "Target path: ${COLOR_CYAN}${INSTALL_PATH}${COLOR_RESET}"

    local fetch_spec="${SOURCE}:.rulesync"
    local original_dir
    local fetch_exit_code=0

    original_dir=$(pwd)

    # Change to target directory for fetch
    cd "$INSTALL_PATH"

    # Clear existing .rulesync directory before fetch
    if [[ -d ".rulesync" ]]; then
        log_info "Removing existing .rulesync directory..."
        rm -rf ".rulesync"
    fi

    # Run rulesync fetch
    log_info "Fetching from ${fetch_spec}..."
    if ! rulesync fetch "$fetch_spec"; then
        log_error "rulesync fetch failed"
        fetch_exit_code=1
    fi

    cd "$original_dir"

    if [[ $fetch_exit_code -ne 0 ]]; then
        exit 1
    fi

    log_success "Fetched .rulesync from ${SOURCE}"
}

run_rulesync_generate() {
    log_step "Running rulesync generate"
    log_info "Targets: ${COLOR_CYAN}${TARGETS}${COLOR_RESET}"

    local original_dir
    local generate_exit_code=0
    local rulesync_jsonc_path
    local delete_from_config

    original_dir=$(pwd)

    # Change to target directory
    cd "$INSTALL_PATH"

    # Verify .rulesync exists
    if [[ ! -d ".rulesync" ]]; then
        log_error ".rulesync directory not found after fetch"
        cd "$original_dir"
        exit 1
    fi

    # Check if rulesync.jsonc exists and has delete: true
    rulesync_jsonc_path=".rulesync/rulesync.jsonc"
    delete_from_config=""
    if [[ -f "$rulesync_jsonc_path" ]]; then
        # Strip C-style comments (// and /* */) and check for delete: true
        delete_from_config=$(sed 's|//.*$||g; :a; s|/\*[^*]*\*\+/||g; ta' "$rulesync_jsonc_path" | grep -oP '"delete"\s*:\s*\K(true|false)' || true)
    fi

    # Define files/directories created by each target
    declare -A target_files
    target_files=(
        ["claudecode"]="AGENTS.md"
        ["copilot"]=".github/prompts"
        ["opencode"]="opencode.jsonc"
        ["geminicli"]="geminicli.jsonc"
        ["factorydroid"]="factory-rules"
        ["codexcli"]="codex.json"
        ["antigravity"]=".antigravity"
    )

    # Check for existing generated files for selected targets
    local existing_files=()
    IFS=',' read -ra target_array <<< "$TARGETS"
    for target in "${target_array[@]}"; do
        # Trim whitespace
        target=$(echo "$target" | xargs)
        if [[ -n "${target_files[$target]:-}" ]]; then
            local file="${target_files[$target]}"
            if [[ -e "$file" ]]; then
                existing_files+=("$file")
            fi
        fi
    done

    # If any generated files exist, ask for confirmation before cleaning
    if [[ ${#existing_files[@]} -gt 0 ]]; then
        log_warning "The following generated files/directories already exist:"
        for file in "${existing_files[@]}"; do
            log_warning "  - $file"
        done
        read -r -p "Remove existing generated files before regenerating? [y/N] " response || true
        if [[ "$response" =~ ^[Yy]$ ]]; then
            log_info "Removing existing generated files..."
            for file in "${existing_files[@]}"; do
                rm -rf "$file"
                log_info "Removed: $file"
            done
        else
            log_info "Proceeding without cleanup (old configurations may persist)"
        fi
    fi

    # Run rulesync generate - use config file when delete: true is set
    if [[ "$delete_from_config" == "true" ]]; then
        log_info "Found delete: true in rulesync.jsonc. Using config file settings..."
        if ! rulesync generate; then
            log_error "rulesync generate failed"
            generate_exit_code=1
        fi
    else
        log_info "Generating files for targets: ${TARGETS}..."
        if ! rulesync generate --targets "$TARGETS" --features "*"; then
            log_error "rulesync generate failed"
            generate_exit_code=1
        fi
    fi

    cd "$original_dir"

    if [[ $generate_exit_code -ne 0 ]]; then
        exit 1
    fi

    log_success "Generated configuration for: ${TARGETS}"
}

download_hooks() {
    log_step "Downloading hook scripts"

    local hooks_dir
    local original_dir
    local download_failed=0

    original_dir=$(pwd)
    hooks_dir="${INSTALL_PATH}/.rulesync/hooks"

    # Create hooks directory if it doesn't exist
    if [[ ! -d "$hooks_dir" ]]; then
        log_info "Creating hooks directory: ${hooks_dir}"
        mkdir -p "$hooks_dir"
    fi

    # Change to target directory for relative paths
    cd "$INSTALL_PATH"

    # Download each hook script
    for script in "${HOOK_SCRIPTS[@]}"; do
        if ! download_hook_script "$script" "$SOURCE" ".rulesync/hooks"; then
            download_failed=1
        fi
    done

    cd "$original_dir"

    if [[ $download_failed -ne 0 ]]; then
        log_error "One or more hook scripts failed to download"
        exit 1
    fi

    log_success "All hook scripts downloaded"
}

make_hooks_executable() {
    log_step "Setting permissions"

    local hooks_dir="${INSTALL_PATH}/.rulesync/hooks"
    local original_dir

    original_dir=$(pwd)

    # Change to target directory
    cd "$INSTALL_PATH"

    # Make downloaded scripts executable
    for script in "${HOOK_SCRIPTS[@]}"; do
        local script_path=".rulesync/hooks/${script}"
        if [[ -f "$script_path" ]]; then
            make_executable "$script_path"
        else
            log_warning "Script not found: ${script_path}"
        fi
    done

    cd "$original_dir"

    log_success "All hook scripts made executable"
}

print_summary() {
    log_step "Installation Summary"

    local install_abs_path
    install_abs_path=$(cd "$INSTALL_PATH" && pwd)

    cat << EOF

${COLOR_GREEN}${COLOR_BOLD}Installation Complete!${COLOR_RESET}

${COLOR_BOLD}Configuration:${COLOR_RESET}
  Source:     ${COLOR_CYAN}${SOURCE}${COLOR_RESET}
  Targets:    ${COLOR_CYAN}${TARGETS}${COLOR_RESET}
  Path:       ${COLOR_CYAN}${install_abs_path}/.rulesync${COLOR_RESET}

${COLOR_BOLD}Hook Scripts Installed:${COLOR_RESET}
$(for script in "${HOOK_SCRIPTS[@]}"; do
    printf "  ${COLOR_GREEN}✓${COLOR_RESET} %s\n" "${script}"
done)

${COLOR_BOLD}Next Steps:${COLOR_RESET}
  1. Review the generated configuration in ${COLOR_CYAN}${install_abs_path}/.rulesync/${COLOR_RESET}
  2. For Copilot users: ensure GitHub Copilot CLI is configured
  3. For Claude Code users: restart your Claude Code session
  4. Run ${COLOR_CYAN}rulesync generate --check${COLOR_RESET} to verify the configuration

${COLOR_BOLD}Documentation:${COLOR_RESET}
  - AGENTS.md: Overview and usage guide
  - .rulesync/hooks/: Hook scripts for automation
  - .rulesync/skills/: Available skill definitions

${COLOR_YELLOW}Note:${COLOR_RESET} If you see "SessionStart:startup hook error",
      the hook scripts are now properly installed and should work.

EOF
}

# =============================================================================
# Main Entry Point
# =============================================================================

main() {
    parse_args "$@"
    check_prerequisites
    run_rulesync_fetch
    run_rulesync_generate
    download_hooks
    make_hooks_executable
    print_summary
}

# Run main function
main "$@"
