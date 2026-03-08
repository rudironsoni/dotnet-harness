# Makefile for Docker-based RuleSync testing
# Provides convenient shortcuts for common operations

.PHONY: help build rebuild test test-claudecode test-opencode test-copilot \
        test-geminicli test-codexcli test-factorydroid test-antigravity \
        determinism doc-contract clean shell validate-all

# Default RuleSync version
RULESYNC_VERSION ?= 7.10.0

# Docker Compose options
COMPOSE_OPTS ?=

# ============================================================================
# Help
# ============================================================================

help:
	@echo "Docker Testing Commands for RuleSync-first .NET Agent Harness"
	@echo ""
	@echo "Usage: make [target]"
	@echo ""
	@echo "Build Commands:"
	@echo "  build           Build all Docker images"
	@echo "  rebuild         Force rebuild all images (no cache)"
	@echo ""
	@echo "Test Commands:"
	@echo "  test            Run full test suite (all targets + validation)"
	@echo "  test-claudecode Test Claude Code target"
	@echo "  test-opencode   Test OpenCode target"
	@echo "  test-copilot    Test GitHub Copilot target"
	@echo "  test-geminicli  Test Gemini CLI target"
	@echo "  test-codexcli   Test Codex CLI target"
	@echo "  test-factorydroid Test Factory Droid target"
	@echo "  test-antigravity Test Antigravity target"
	@echo ""
	@echo "Validation Commands:"
	@echo "  determinism     Run determinism check"
	@echo "  doc-contract    Validate documentation contract"
	@echo "  validate-all    Run all validation checks"
	@echo ""
	@echo "Utility Commands:"
	@echo "  shell           Start interactive shell"
	@echo "  clean           Clean up containers and volumes"
	@echo ""
	@echo "Environment Variables:"
	@echo "  RULESYNC_VERSION  RuleSync version to use (default: 7.10.0)"
	@echo "  COMPOSE_OPTS      Additional docker-compose options"

# ============================================================================
# Build
# ============================================================================

build:
	@echo "Building Docker images with RuleSync $(RULESYNC_VERSION)..."
	docker-compose build $(COMPOSE_OPTS) --build-arg RULESYNC_VERSION=$(RULESYNC_VERSION)

rebuild:
	@echo "Force rebuilding Docker images..."
	docker-compose build --no-cache $(COMPOSE_OPTS) --build-arg RULESYNC_VERSION=$(RULESYNC_VERSION)

# ============================================================================
# Test Commands
# ============================================================================

test: validate-all
	@echo "All tests completed!"

test-claudecode:
	@echo "Testing Claude Code target..."
	docker-compose run --rm claudecode

test-opencode:
	@echo "Testing OpenCode target..."
	docker-compose run --rm opencode

test-copilot:
	@echo "Testing GitHub Copilot target..."
	docker-compose run --rm copilot

test-geminicli:
	@echo "Testing Gemini CLI target..."
	docker-compose run --rm geminicli

test-codexcli:
	@echo "Testing Codex CLI target..."
	docker-compose run --rm codexcli

test-factorydroid:
	@echo "Testing Factory Droid target..."
	docker-compose run --rm factorydroid

test-antigravity:
	@echo "Testing Antigravity target..."
	docker-compose run --rm antigravity

# ============================================================================
# Validation Commands
# ============================================================================

determinism:
	@echo "Running determinism check..."
	docker-compose run --rm determinism-check

doc-contract:
	@echo "Validating documentation contract..."
	docker-compose run --rm doc-contract

validate-all:
	@echo "Running full validation suite..."
	docker-compose run --rm validate

# ============================================================================
# Utility Commands
# ============================================================================

shell:
	@echo "Starting interactive shell..."
	docker-compose run --rm shell

clean:
	@echo "Cleaning up Docker containers and volumes..."
	docker-compose down -v --remove-orphans
	@echo "Cleanup complete!"

clean-all: clean
	@echo "Removing built images..."
	docker-compose rm -f
	docker rmi rulesync-test-runner:latest 2>/dev/null || true
	@echo "All resources cleaned!"

# ============================================================================
# Batch Testing
# ============================================================================

test-all-targets: test-claudecode test-opencode test-copilot test-geminicli test-codexcli test-factorydroid test-antigravity
	@echo "All individual target tests completed!"

# ============================================================================
# CI Simulation
# ============================================================================

ci-test:
	@echo "Simulating CI pipeline locally..."
	$(MAKE) build
	$(MAKE) test-all-targets
	$(MAKE) determinism
	$(MAKE) doc-contract
	$(MAKE) validate-all
	@echo "CI simulation complete!"
