FROM mcr.microsoft.com/devcontainers/dotnet:1-10.0-bookworm

# Remove the yarn repository with expired/missing GPG key
RUN rm -f /etc/apt/sources.list.d/yarn.list

# Update package lists
RUN apt-get update && apt-get install -y --no-install-recommends \
    curl \
    wget \
    ca-certificates \
    && rm -rf /var/lib/apt/lists/*

# Install Claude CLI
RUN curl -fsSL https://claude.ai/install.sh | bash

# Set up working directory
WORKDIR /workspace

# Ensure dotnet tools are available
ENV PATH="${PATH}:/root/.dotnet/tools:${HOME}/.local/bin"

