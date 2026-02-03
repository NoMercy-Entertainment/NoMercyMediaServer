FROM mcr.microsoft.com/devcontainers/dotnet:1-9.0-bookworm

# Remove the yarn repository with expired/missing GPG key
RUN rm -f /etc/apt/sources.list.d/yarn.list

# Update package lists
RUN apt-get update && apt-get install -y --no-install-recommends \
    curl \
    wget \
    ca-certificates \
    && rm -rf /var/lib/apt/lists/*

# Install .NET 10 SDK (preview)
RUN curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --channel 10.0 --install-dir /usr/share/dotnet

# Install Claude CLI
RUN curl -fsSL https://claude.ai/install.sh | bash

# Set up working directory
WORKDIR /workspace

# Ensure dotnet tools are available
ENV PATH="${PATH}:/root/.dotnet/tools:${HOME}/.local/bin"

