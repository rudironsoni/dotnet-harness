#!/usr/bin/env bash
# Docker entrypoint script for RuleSync test runner
# Routes commands to appropriate test scripts

set -euo pipefail

# Color codes for output
readonly RED='\033[0;31m'
readonly GREEN='\033[0;32m'
readonly YELLOW='\033[1;33m'
readonly BLUE='\033[0;34m'
readonly NC='\033[0m' # No Color

# Logging functions
log_info() { echo -e "${BLUE}[INFO]${NC} $*"; }
log_success() { echo -e "${GREEN}[SUCCESS]${NC} $*"; }
log_warn() { echo -e "${YELLOW}[WARN]${NC} $*"; }
log_error() { echo -e "${RED}[ERROR]${NC} $*"; }

# Show usage information
show_help() {
    cat << 'EOF'
RuleSync Test Runner - Docker Entrypoint

Usage:
  docker-compose run <service> [COMMAND]

Available Commands:
  validate-target [TARGET]     Validate generation for specific target
  validate-all                 Validate generation for all targets
  check-determinism           Check RuleSync generation determinism
  validate-doc-contract       Validate documentation contract
  test-skills                 Test skills generation across targets
  test-commands               Test commands generation across targets
  test-subagents              Test subagents generation across targets
  shell                       Start interactive shell
  --help, -h                  Show this help message

Environment Variables:
  TARGET                      Target to test (claudecode, opencode, etc.)
  RULESYNC_TARGETS            Comma-separated list of targets
  RULESYNC_FEATURES           Comma-separated list of features (skills,commands,subagents)
  CI                          Set to 'true' for CI mode

Examples:
  # Validate single target
  docker-compose run claudecode validate-target

  # Validate all targets
  docker-compose run validate

  # Check determinism
  docker-compose run determinism-check

  # Interactive shell
  docker-compose run shell

EOF
}

# Main entrypoint logic
main() {
    # If no arguments provided, show help
    if [[ $# -eq 0 ]]; then
        show_help
        exit 0
    fi

    local command="$1"
    shift || true

    case "$command" in
        --help|-h)
            show_help
            exit 0
            ;;

        validate-target)
            exec /usr/local/bin/validate-target "$@"
            ;;

        validate-all)
            exec /usr/local/bin/validate-all
            ;;

        check-determinism)
            exec /usr/local/bin/check-determinism
            ;;

        validate-doc-contract)
            exec /usr/local/bin/validate-doc-contract
            ;;

        test-skills)
            export RULESYNC_FEATURES="skills"
            exec /usr/local/bin/validate-target
            ;;

        test-commands)
            export RULESYNC_FEATURES="commands"
            exec /usr/local/bin/validate-target
            ;;

        test-subagents)
            export RULESYNC_FEATURES="subagents"
            exec /usr/local/bin/validate-target
            ;;

        shell|bash|sh)
            log_info "Starting interactive shell..."
            exec /bin/bash
            ;;

        rulesync)
            # Pass through to rulesync CLI
            exec rulesync "$@"
            ;;

        *)
            # Check if it's a valid script in /usr/local/bin
            if [[ -x "/usr/local/bin/$command" ]]; then
                exec "/usr/local/bin/$command" "$@"
            fi

            # Check if it's a shell command
            if command -v "$command" &> /dev/null; then
                exec "$command" "$@"
            fi

            log_error "Unknown command: $command"
            echo ""
            show_help
            exit 1
            ;;
    esac
}

# Run main function
main "$@"
