# justfile for dotnet-agent-harness

set shell := ["bash", "-c"]

default:
    @just --list

# --- Setup ---

# Install tools required to work on the project
setup:
    @echo "Checking prerequisites..."
    @which act > /dev/null || (echo "Please install act (https://github.com/nektos/act)"; exit 1)
    @which yq > /dev/null || (echo "Please install yq (https://github.com/mikefarah/yq)"; exit 1)
    @which dotnet > /dev/null || (echo "Please install .NET SDK"; exit 1)
    @which just > /dev/null || (echo "Please install just"; exit 1)
    @which mdl > /dev/null || echo "Note: mdl not found - Markdown linting will be skipped"
    @which codespell > /dev/null || echo "Note: codespell not found - spell checking will be skipped"
    @which shellcheck > /dev/null || echo "Note: shellcheck not found - shell script linting will be skipped"
    @which rulesync > /dev/null || (echo "Please install rulesync (scripts/ci/install_rulesync.sh)"; exit 1)
    @echo "Setup complete."

# --- Code Generation & Validation ---

# Validate subagents
validate-subagents:
    bash scripts/ci/validate_subagents.sh

# Validate rulesync outputs
validate-rulesync:
    bash scripts/ci/validate_rulesync.sh

# Validate documentation capability contract
validate-doc-contract:
    bash scripts/ci/validate_doc_contract.sh

# Generate rulesync artifacts
generate:
    rulesync generate

# Run CI rulesync checks
ci-rulesync: lint validate-subagents validate-doc-contract validate-rulesync

# --- Linting & Formatting ---

# Run all linters
lint: lint-md lint-frontmatter lint-spell lint-shell
    @echo "Linting passed."

lint-md:
    @which mdl > /dev/null && mdl -i node_modules -i src -i packages . || echo "Skipping lint-md (mdl not installed)"

lint-frontmatter:
    @echo "Skipping lint-frontmatter (no frontmatter linter available)"

lint-spell:
    @which codespell > /dev/null && codespell -q 3 --skip="./.git,./.opencode,./.claude,./.gemini,./.codex,./.agent,./.vscode,./dist,./node_modules,./packages" || echo "Skipping lint-spell (codespell not installed)"

lint-shell:
    @which shellcheck > /dev/null && shellcheck scripts/**/*.sh || echo "Skipping lint-shell (shellcheck not installed)"

format:
    dotnet csharpier src/

# --- CI Equivalency ---

# Run GitHub actions locally using act
act:
    act -j rulesync-validate
    act -j lint
