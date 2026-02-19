# NoMercy MediaServer Dockerfile
# Multi-stage build for optimal image size
#
# FFmpeg is downloaded by the MediaServer at runtime with appropriate
# hardware acceleration support for the platform.

# =============================================================================
# Stage 1: Build
# =============================================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files first (better layer caching)
COPY NoMercy.Server.sln ./
COPY src/NoMercy.Service/NoMercy.Service.csproj src/NoMercy.Service/
COPY src/NoMercy.Api/NoMercy.Api.csproj src/NoMercy.Api/
COPY src/NoMercy.App/NoMercy.App.csproj src/NoMercy.App/
COPY src/NoMercy.Data/NoMercy.Data.csproj src/NoMercy.Data/
COPY src/NoMercy.Database/NoMercy.Database.csproj src/NoMercy.Database/
COPY src/NoMercy.Encoder/NoMercy.Encoder.csproj src/NoMercy.Encoder/
COPY src/NoMercy.Events/NoMercy.Events.csproj src/NoMercy.Events/
COPY src/NoMercy.Globals/NoMercy.Globals.csproj src/NoMercy.Globals/
COPY src/NoMercy.Helpers/NoMercy.Helpers.csproj src/NoMercy.Helpers/
COPY src/NoMercy.MediaProcessing/NoMercy.MediaProcessing.csproj src/NoMercy.MediaProcessing/
COPY src/NoMercy.MediaSources/NoMercy.MediaSources.csproj src/NoMercy.MediaSources/
COPY src/NoMercy.Networking/NoMercy.Networking.csproj src/NoMercy.Networking/
COPY src/NoMercy.NmSystem/NoMercy.NmSystem.csproj src/NoMercy.NmSystem/
COPY src/NoMercy.Plugins/NoMercy.Plugins.csproj src/NoMercy.Plugins/
COPY src/NoMercy.Plugins.Abstractions/NoMercy.Plugins.Abstractions.csproj src/NoMercy.Plugins.Abstractions/
COPY src/NoMercy.Providers/NoMercy.Providers.csproj src/NoMercy.Providers/
COPY src/NoMercy.Queue.MediaServer/NoMercy.Queue.MediaServer.csproj src/NoMercy.Queue.MediaServer/
COPY src/NoMercy.Setup/NoMercy.Setup.csproj src/NoMercy.Setup/

# Restore dependencies
RUN dotnet restore src/NoMercy.Service/NoMercy.Service.csproj

# Copy everything else
COPY src/ src/
COPY assets/ assets/

# Build and publish
RUN dotnet publish src/NoMercy.Service/NoMercy.Service.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    -p:PublishSingleFile=false \
    -p:DebugType=None \
    -p:DebugSymbols=false

# =============================================================================
# Stage 2: Runtime (CPU only)
# =============================================================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Install minimal dependencies (FFmpeg is downloaded by MediaServer)
RUN apt-get update && apt-get install -y --no-install-recommends \
    # Required for FFmpeg runtime
    libva2 \
    libva-drm2 \
    libdrm2 \
    # Media info
    mediainfo \
    # Utilities
    curl \
    ca-certificates \
    && rm -rf /var/lib/apt/lists/*

# Create non-root user for security
RUN useradd -m -u 1000 nomercy

# Create data directories
RUN mkdir -p /data/config /data/cache /data/media /data/log \
    && chown -R nomercy:nomercy /data

# Copy published application
COPY --from=build /app/publish .

# Set ownership
RUN chown -R nomercy:nomercy /app

# Switch to non-root user
USER nomercy

# Environment variables
ENV ASPNETCORE_URLS=http://+:7626
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV HOME=/data

# Expose port
EXPOSE 7626

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
    CMD curl -f http://localhost:7626/health || exit 1

# Entry point
ENTRYPOINT ["dotnet", "NoMercyMediaServer.dll"]
CMD ["--internal-port", "7626"]
