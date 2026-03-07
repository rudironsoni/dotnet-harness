#!/usr/bin/env bash
# Docker-based test runner for RuleSync-first .NET agent harness
# Usage: ./scripts/ci/docker-test.sh [options] [command]

set -euo pipefail

# Configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
RULESYNC_VERSION="${RULESYNC_VERSION:-7.10.0}"

# Color codes
readonly RED='\033[0;31m'
readonly GREEN='\033[0;32m'
readonly YELLOW='\033[1;33m'
readonly BLUE='\033[0;34m'
readonly CYAN='\033[0;36m'
readonly NC='\033[0m'

# Logging functions
log_info() { echo -e "${BLUE}[INFO]${NC} $*"; }
log_success() { echo -e "${GREEN}[SUCCESS]${NC} $*"; }
log_warn() { echo -e "${YELLOW}[WARN]${NC} $*"; }
log_error() { echo -e "${RED}[ERROR]${NC} $*"; }
log_step() { echo -e "${CYAN}[STEP]${NC} $*"; }

# Show usage
show_usage() {
    cat << 'EOF'
Docker Test Runner for RuleSync-first .NET Agent Harness

Usage:
  ./scripts/ci/docker-test.sh [OPTIONS] [COMMAND]

Commands:
  all                 Run all tests (default)
  validate            Run validation suite
  determinism         Run determinism check only
  doc-contract        Validate documentation contract
  target TARGET       Test specific target (claudecode, opencode, etc.)
  build               Build Docker images only
  clean               Clean up Docker resources
  shell               Start interactive shell

Options:
  -v, --version VER   Use specific RuleSync version (default: 7.10.0)
  -r, --rebuild       Force rebuild Docker images
  -p, --parallel      Run tests in parallel (faster but more resource intensive)
  -h, --help          Show this help message

Examples:
  # Run all tests
  ./scripts/ci/docker-test.sh

  # Test specific target
  ./scripts/ci/docker-test.sh target claudecode

  # Test with specific RuleSync version
  ./scripts/ci/docker-test.sh -v 7.9.0 all

  # Run determinism check
  ./scripts/ci/docker-test.sh determinism

  # Interactive shell
  ./scripts/ci/docker-test.sh shell

EOF
}

# Parse arguments
PARALLEL=false
REBUILD=false
COMMAND="all"

while [[ $# -gt 0 ]]; do
    case $1 in
        -v|--version)
            RULESYNC_VERSION="$2"
            shift 2
            ;;
        -r|--rebuild)
            REBUILD=true
            shift
            ;;
        -p|--parallel)
            PARALLEL=true
            shift
            ;;
        -h|--help)
            show_usage
            exit 0
            ;;
        all|validate|determinism|doc-contract|target|build|clean|shell)
            COMMAND="$1"
            shift
            break
            ;;
        *)
            log_error "Unknown option: $1"
            show_usage
            exit 1
            ;;
    esac
done

# Change to repo root
cd "$REPO_ROOT"

# Check Docker is available
check_docker() {
    if ! command -v docker &> /dev/null; then
        log_error "Docker is not installed"
        exit 1
    fi

    if ! command -v docker-compose &> /dev/null; then
        log_error "Docker Compose is not installed"
        exit 1
    fi

    if ! docker info &> /dev/null; then
        log_error "Docker daemon is not running"
        exit 1
    fi

    log_info "Docker version: $(docker --version)"
    log_info "Docker Compose version: $(docker-compose --version)"
}

# Build Docker images
build_images() {
    log_step "Building Docker images with RuleSync $RULESYNC_VERSION..."

    if [[ "$REBUILD" == true ]]; then
        docker-compose build --no-cache --build-arg RULESYNC_VERSION="$RULESYNC_VERSION"
    else
        docker-compose build --build-arg RULESYNC_VERSION="$RULESYNC_VERSION"
    fi

    log_success "Docker images built successfully"
}

# Run test for a single target
test_target() {
    local target="$1"
    log_step "Testing target: $target..."

    if docker-compose run --rm "$target"; then
        log_success "Target $target: PASSED"
        return 0
    else
        log_error "Target $target: FAILED"
        return 1
    fi
}

# Run all target tests
run_all_tests() {
    local failed=0
    local targets=(claudecode opencode copilot geminicli codexcli factorydroid antigravity)

    log_step "Running tests for all targets..."
    echo ""

    if [[ "$PARALLEL" == true ]]; then
        log_info "Running tests in parallel..."
        for target in "${targets[@]}"; do
            test_target "$target" &
        done
        wait
    else
        for target in "${targets[@]}"; do
            if ! test_target "$target"; then
                failed=1
            fi
            echo ""
        done
    fi

    return $failed
}

# Run determinism check
run_determinism() {
    log_step "Running determinism check..."

    if docker-compose run --rm determinism-check; then
        log_success "Determinism check: PASSED"
        return 0
    else
        log_error "Determinism check: FAILED"
        return 1
    fi
}

# Run documentation contract validation
run_doc_contract() {
    log_step "Validating documentation contract..."

    if docker-compose run --rm doc-contract; then
        log_success "Documentation contract: PASSED"
        return 0
    else
        log_error "Documentation contract: FAILED"
        return 1
    fi
}

# Run full validation suite
run_validate_all() {
    log_step "Running full validation suite..."

    if docker-compose run --rm validate; then
        log_success "Full validation: PASSED"
        return 0
    else
        log_error "Full validation: FAILED"
        return 1
    fi
}

# Clean up Docker resources
clean_resources() {
    log_step "Cleaning up Docker resources..."

    docker-compose down -v --remove-orphans
    docker-compose rm -f

    # Remove images
    docker rmi rulesync-test-runner:latest 2>/dev/null || true

    log_success "Cleanup complete"
}

# Start interactive shell
start_shell() {
    log_step "Starting interactive shell..."
    docker-compose run --rm shell
}

# Main execution
main() {
    echo ""
    log_info "=========================================="
    log_info "Docker Test Runner"
    log_info "RuleSync Version: $RULESYNC_VERSION"
    log_info "=========================================="
    echo ""

    check_docker

    case "$COMMAND" in
        build)
            build_images
            ;;
        all)
            build_images
            run_all_tests
            run_determinism
            run_doc_contract
            run_validate_all
            ;;
        validate)
            build_images
            run_validate_all
            ;;
        determinism)
            build_images
            run_determinism
            ;;
        doc-contract)
            build_images
            run_doc_contract
            ;;
        target)
            local target="${1:-}"
            if [[ -z "$target" ]]; then
                log_error "No target specified. Usage: $0 target <target_name>"
                exit 1
            fi
            build_images
            test_target "$target"
            ;;
        clean)
            clean_resources
            ;;
        shell)
            start_shell
            ;;
        *)
            log_error "Unknown command: $COMMAND"
            show_usage
            exit 1
            ;;
    esac

    echo ""
    log_success "=========================================="
    log_success "Docker testing complete!"
    log_success "=========================================="
}

# Run main
main "$@"
