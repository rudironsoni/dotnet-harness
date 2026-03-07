# Dockerfile for dotnet-agent-harness toolkit testing infrastructure
# Base image with latest RuleSync (unpinned - always gets latest)

FROM ubuntu:22.04 AS base

# Prevent interactive prompts during package installation
ENV DEBIAN_FRONTEND=noninteractive

# Install base dependencies
RUN apt-get update && apt-get install -y --no-install-recommends \
    curl \
    ca-certificates \
    git \
    jq \
    shellcheck \
    bc \
    && rm -rf /var/lib/apt/lists/*

# Install latest RuleSync (always fetches latest release, no version pinning)
RUN curl -fsSL https://github.com/dyoshikawa/rulesync/releases/latest/download/rulesync-linux-x64 -o /usr/local/bin/rulesync \
    && chmod +x /usr/local/bin/rulesync

# Copy entrypoint script for version checking
COPY scripts/docker/entrypoint.sh /usr/local/bin/entrypoint.sh
RUN chmod +x /usr/local/bin/entrypoint.sh

# Create non-root user for security
RUN groupadd -g 1001 rulesync \
    && useradd -u 1001 -g rulesync -m -s /bin/bash rulesync

# Set working directory
WORKDIR /workspace

# Health check to verify RuleSync is available
HEALTHCHECK --interval=30s --timeout=5s --start-period=5s --retries=3 \
    CMD rulesync --version || exit 1

# Use entrypoint for version logging and command execution
ENTRYPOINT ["/usr/local/bin/entrypoint.sh"]

# Default command shows version
CMD ["rulesync", "--version"]

# Labels for image metadata
LABEL org.opencontainers.image.title="dotnet-agent-harness"
LABEL org.opencontainers.image.description="RuleSync-first testing infrastructure for dotnet-agent-harness"
LABEL org.opencontainers.image.source="https://github.com/rudironsoni/dotnet-agent-harness"
LABEL org.opencontainers.image.licenses="MIT"
