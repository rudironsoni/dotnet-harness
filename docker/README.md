# Docker Testing Infrastructure

Docker-first testing infrastructure for the RuleSync-first .NET agent harness toolkit.

## Overview

This infrastructure provides:

- **Base Image**: Ubuntu 22.04 with latest RuleSync installed (unpinned)
- **Multi-Target Testing**: Parallel testing across 7 AI agent targets
- **Validation Suite**: Comprehensive validation of skills, commands, and subagents
- **CI/CD Integration**: GitHub Actions workflow for automated testing

## Quick Start

### Run All Tests Locally

```bash
# Build the base image
docker-compose build base

# Run the full test suite
docker-compose run --rm test-runner

# Or run tests with custom output directory
docker-compose run --rm test-runner --output /custom/output
```

### Test Single Target

```bash
# Test a specific target
docker-compose run --rm claudecode
docker-compose run --rm opencode
docker-compose run --rm copilot
docker-compose run --rm geminicli
docker-compose run --rm codexcli
docker-compose run --rm antigravity
docker-compose run --rm factorydroid
```

### Run Validation Only

```bash
# Run validation suite
docker-compose run --rm base /workspace/scripts/docker/validate-all.sh
```

## Available Commands

### Master Test Script

```bash
docker-compose run --rm test-runner [options]

Options:
  --output <dir>     Output directory for test results (default: /test-results)
  --verbose          Enable verbose output
  --target <name>    Test specific target only
```

### Direct Docker Usage

```bash
# Run with base image directly
docker run --rm \
  -v $(pwd):/workspace:ro \
  ghcr.io/rudironsoni/dotnet-agent-harness-test-base:latest \
  rulesync --version

# Run validation
docker run --rm \
  -v $(pwd):/workspace:ro \
  ghcr.io/rudironsoni/dotnet-agent-harness-test-base:latest \
  /workspace/scripts/docker/validate-all.sh

# Run specific target generation
docker run --rm \
  -v $(pwd):/workspace:ro \
  -v $(pwd)/generated:/generated \
  ghcr.io/rudironsoni/dotnet-agent-harness-test-base:latest \
  sh -c "rulesync generate --targets claudecode --output /generated"
```

## Debugging

### Check RuleSync Version

```bash
docker-compose run --rm base rulesync --version
```

### Inspect Generated Output

```bash
# Generate output for a target
docker-compose run --rm claudecode

# The output is stored in a Docker volume
# To inspect it:
docker run --rm -v generated-claudecode:/generated alpine ls -la /generated
```

### Shell Access

```bash
# Get a shell in the base container
docker-compose run --rm base bash

# As non-root user
docker-compose run --rm base bash -c "su - rulesync"
```

### View Test Logs

```bash
# Run tests and save logs
docker-compose run --rm test-runner --verbose

# Or check individual test logs
docker run --rm -v test-results:/test-results alpine cat /test-results/json-validation.log
```

## CI/CD Integration

### GitHub Actions

The workflow `.github/workflows/docker-test.yml` runs on:

- Push to `main` or `develop` branches
- Pull requests to `main`
- Manual trigger (`workflow_dispatch`)

Jobs:

1. **build-base**: Builds and pushes base image to GHCR
2. **test-matrix**: Runs tests for each of 7 targets in parallel
3. **validate**: Runs full validation suite
4. **test-suite**: Runs comprehensive test suite
5. **summary**: Aggregates results

### Artifacts

- Generated output for each target
- Test results (JSON format)
- Validation reports

## Architecture

### Images

| Image | Purpose | Base |
|-------|---------|------|
| `test-base` | Foundation with RuleSync | ubuntu:22.04 |
| `test-runner` | Test execution | test-base |

### Volumes

| Volume | Purpose |
|--------|---------|
| `generated-*` | Target-specific generated output |
| `test-results` | Test results and logs |

### Services

- **base**: Foundation image with RuleSync
- **test-runner**: Runs validation and test suites
- **{target}**: Individual target testing (7 services)

## Validation Checks

The `validate-all.sh` script verifies:

1. **Skill Count**: Exactly 189 skills
2. **Command Count**: Exactly 27 commands
3. **Subagent Count**: Exactly 15 subagents
4. **JSON Validity**: All JSON files are valid
5. **Hook Scripts**: All hooks are executable and pass shellcheck
6. **Skill Structure**: All skills have SKILL.md with frontmatter
7. **Configuration**: rulesync.jsonc has all 7 targets and required fields

## Test Results Format

Results are output as JSON:

```json
{
  "testRun": {
    "startTime": "2024-01-01T00:00:00Z",
    "duration": 45.123,
    "rulesyncVersion": "1.2.3"
  },
  "summary": {
    "total": 10,
    "passed": 10,
    "failed": 0,
    "successRate": 100.00
  },
  "tests": [
    {
      "name": "json-validation",
      "status": "PASSED",
      "duration": 0.523
    }
  ]
}
```

## Troubleshooting

### RuleSync Not Found

```bash
# Verify installation
docker-compose run --rm base which rulesync
docker-compose run --rm base rulesync --version
```

### Permission Errors

The base image runs as non-root user `rulesync` (UID 1001) for security.

```bash
# Check permissions
docker-compose run --rm base id
```

### Volume Mount Issues

```bash
# Ensure proper permissions
chmod -R +r .rulesync/

# Or use ACL (Linux)
setfacl -R -m u:1001:rX .rulesync/
```

### Network Issues

```bash
# Test network connectivity
docker-compose run --rm base curl -I https://github.com
```

## Development

### Updating RuleSync

RuleSync is installed from the latest release (unpinned). To update:

```bash
# Rebuild the base image
docker-compose build --no-cache base
```

### Adding New Tests

1. Add test functions to `scripts/docker/test.sh`
2. Register tests in the main execution section
3. Update this documentation

### Adding New Targets

1. Add target to `rulesync.jsonc`
2. Add service to `docker-compose.yml`
3. Update GitHub Actions matrix
4. Update validation checks

## Related Documentation

- [RuleSync Documentation](.rulesync/skills/rulesync/SKILL.md)
- [Project README](../README.md)
- [CI/CD Scripts](../scripts/ci/)
