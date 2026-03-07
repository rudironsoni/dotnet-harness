#!/bin/bash
set -e

# Docker entrypoint script for dotnet-agent-harness
# Provides version logging and command delegation

SCRIPT_VERSION="1.0.0"

echo "=========================================="
echo "  dotnet-agent-harness Docker Container"
echo "  Version: ${SCRIPT_VERSION}"
echo "  Image: ${IMAGE_NAME:-dotnet-agent-harness}"
echo "  Built: ${BUILD_DATE:-unknown}"
echo "=========================================="
echo ""

# Log environment info
echo "Environment:"
echo "  Working Directory: $(pwd)"
echo "  .NET Version: $(dotnet --version 2>/dev/null || echo 'N/A')"
echo ""

# Execute the command passed to the container
exec "$@"
