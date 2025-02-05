FROM mcr.microsoft.com/dotnet/runtime:9.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["NoMercy.Updater/NoMercy.Updater.csproj", "NoMercy.Updater/"]
RUN dotnet restore "NoMercy.Updater/NoMercy.Updater.csproj"
COPY . .
WORKDIR "/src/NoMercy.Updater"
RUN dotnet build "NoMercy.Updater.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "NoMercy.Updater.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "NoMercy.Updater.dll"]
