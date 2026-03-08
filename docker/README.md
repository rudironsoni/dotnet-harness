# Docker Testing for RuleSync-first .NET Agent Harness

This directory contains Docker-based testing infrastructure for the RuleSync-first .NET agent harness toolkit.

## Overview

The Docker testing infrastructure provides:

- **Isolation**: Each of the 7 targets runs in its own container
- **Reproducibility**: Pinned RuleSync versions ensure consistent results
- **Efficiency**: Volume mounts and caching for fast iteration
- **Simplicity**: Single command to run all tests
- **CI/CD Ready**: Integrated with GitHub Actions

## Quick Start

### Prerequisites

- Docker Engine 24.0+
- Docker Compose 2.20+
- 4GB+ available RAM for parallel testing

### Run All Tests

```bash
# Build images and run complete test suite
docker-compose up validate

# Or run in detached mode
docker-compose up -d validate
```

### Test Individual Target

```bash
# Test Claude Code target
docker-compose up claudecode

# Test OpenCode target
docker-compose up opencode

# Test GitHub Copilot target
docker-compose up copilot
```

## Available Services

### Individual Target Services

| Service | Description |
|---------|-------------|
| `claudecode` | Test Claude Code target generation |
| `opencode` | Test OpenCode target generation |
| `copilot` | Test GitHub Copilot target generation |
| `geminicli` | Test Gemini CLI target generation |
| `codexcli` | Test Codex CLI target generation |
| `factorydroid` | Test Factory Droid target generation |
| `antigravity` | Test Antigravity target generation |

### Multi-Target Services

| Service | Description |
|---------|-------------|
| `all-targets` | Test all 7 targets sequentially |
| `skills-test` | Test only skills generation |
| `commands-test` | Test only commands generation |
| `subagents-test` | Test only subagents generation |

### Validation Services

| Service | Description |
|---------|-------------|
| `validate` | Full validation suite (same as CI) |
| `determinism-check` | Verify deterministic generation |
| `doc-contract` | Validate documentation contract |

### Utility Services

| Service | Description |
|---------|-------------|
| `shell` | Interactive shell for debugging |
| `rulesync-cli` | Run RuleSync CLI interactively |

## Volume Mounts

The following directories are mounted for efficient testing:

```yaml
volumes:
  # Source code (live editing)
  - .:/workspace:cached

  # NPM cache (persistent)
  - rulesync-npm-cache:/root/.npm

  # RuleSync cache (persistent)
  - rulesync-install-cache:/root/.cache/rulesync

  # NuGet cache (for .NET builds)
  - nuget-cache:/root/.nuget/packages
```

## Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `TARGET` | - | Single target to test |
| `RULESYNC_TARGETS` | - | Comma-separated list of targets |
| `RULESYNC_FEATURES` | `*` | Features to generate (skills,commands,subagents) |
| `RULESYNC_VERSION` | `7.10.0` | RuleSync version to install |
| `CI` | `true` | CI mode for consistent output |

## Test Commands

### Validate Single Target

```bash
docker-compose run claudecode validate-target
```

### Validate All Targets

```bash
docker-compose run validate
```

### Check Determinism

```bash
docker-compose run determinism-check
```

### Test Skills Only

```bash
docker-compose run skills-test
```

### Interactive Shell

```bash
docker-compose run shell
```

## Custom RuleSync Version

Test with a specific RuleSync version:

```bash
# Build with specific version
docker-compose build --build-arg RULESYNC_VERSION=7.9.0

# Or set via environment
docker-compose build --build-arg RULESYNC_VERSION=7.9.0 all-targets
```

## CI/CD Integration

### GitHub Actions

The workflow `.github/workflows/docker-test.yml` runs automatically on PRs:

```yaml
on:
  pull_request:
    paths:
      - '.rulesync/**'
      - 'rulesync.jsonc'
```

### Manual Trigger

You can manually trigger the workflow with custom parameters:

1. Go to **Actions** → **Docker Test**
2. Click **Run workflow**
3. Specify RuleSync version and targets

### Local CI Simulation

Simulate CI locally:

```bash
# Run the exact same commands as CI
docker-compose run -e CI=true validate
docker-compose run -e CI=true determinism-check
docker-compose run -e CI=true doc-contract
```

## Troubleshooting

### Permission Issues

If you encounter permission errors:

```bash
# Fix ownership
sudo chown -R $(id -u):$(id -g) .

# Or run with user mapping
docker-compose run --user $(id -u):$(id -g) shell
```

### Cache Issues

Clear caches and rebuild:

```bash
docker-compose down -v
docker-compose build --no-cache
docker-compose up validate
```

### Network Issues

If services can't communicate:

```bash
# Recreate network
docker-compose down
docker network prune -f
docker-compose up validate
```

### Slow Builds

Enable BuildKit for faster builds:

```bash
export DOCKER_BUILDKIT=1
export COMPOSE_DOCKER_CLI_BUILD=1
docker-compose build
```

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                      Docker Compose                            │
├─────────────────────────────────────────────────────────────────┤
│ ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐             │
│ │claudecode│ │ opencode │ │  copilot │ │geminicli │  ...        │
│ └────┬─────┘ └────┬─────┘ └────┬─────┘ └────┬─────┘             │
│      │            │            │            │                  │
│      └────────────┴────────────┴────────────┘                  │
│                         │                                       │
│              ┌──────────┴──────────┐                              │
│              │   Test Runner      │                              │
│              │   (Dockerfile.test)│                              │
│              │   - RuleSync       │                              │
│              │   - Node.js 20     │                              │
│              │   - Python 3       │                              │
│              │   - jq, yq, etc.   │                              │
│              └──────────┬──────────┘                              │
│                         │                                       │
│              ┌──────────┴──────────┐                              │
│              │   Volume Mounts     │                              │
│              │   - Source code    │                              │
│              │   - NPM cache      │                              │
│              │   - NuGet cache    │                              │
│              └────────────────────┘                              │
└─────────────────────────────────────────────────────────────────┘
```

## Development

### Adding New Test Scripts

1. Create script in `docker/test-scripts/`
2. Make it executable: `chmod +x docker/test-scripts/my-script.sh`
3. Add symlink in `Dockerfile.test`
4. Update `docker/test-entrypoint.sh` to route the command

### Modifying the Base Image

Edit `Dockerfile.test` and rebuild:

```bash
docker-compose build --no-cache
```

## Contributing

When contributing Docker-related changes:

1. Test locally: `docker-compose up validate`
2. Check all targets: `docker-compose run validate-all`
3. Verify determinism: `docker-compose run determinism-check`

## References

- [Docker Compose Documentation](https://docs.docker.com/compose/)
- [RuleSync Documentation](https://github.com/dyoshikawa/rulesync)
- [GitHub Actions Documentation](https://docs.github.com/actions)
