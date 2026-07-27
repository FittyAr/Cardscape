# syntax=docker/dockerfile:1.7
# Multi-stage build for the Cardscape API. Targets a .NET 11 preview SDK
# and produces a self-contained ASP.NET Core image.

# ── Stage 1: restore + publish ──────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:11.0-preview AS build
WORKDIR /src

# Copy build configuration first so the package restore layer caches.
COPY Directory.Build.props Directory.Packages.props global.json ./
COPY src/Cardscape.Domain/Cardscape.Domain.csproj           src/Cardscape.Domain/
COPY src/Cardscape.Application/Cardscape.Application.csproj src/Cardscape.Application/
COPY src/Cardscape.Infrastructure/Cardscape.Infrastructure.csproj src/Cardscape.Infrastructure/
COPY src/Cardscape.Api/Cardscape.Api.csproj                 src/Cardscape.Api/

RUN dotnet restore src/Cardscape.Api/Cardscape.Api.csproj

# Copy the rest of the source and publish.
COPY src/ src/
RUN dotnet publish src/Cardscape.Api/Cardscape.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

# ── Stage 2: runtime ────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:11.0-preview AS runtime
WORKDIR /app

# Non-root for safety; UID 1000 matches the .NET base image convention.
RUN groupadd --system --gid 1001 cardscape \
    && useradd  --system --uid 1001 --gid cardscape cardscape \
    && mkdir -p /app/Data /app/Storage \
    && chown -R cardscape:cardscape /app

COPY --from=build --chown=cardscape:cardscape /app/publish ./

USER cardscape

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV Database__Provider=Sqlite
ENV ConnectionStrings__Default=Data Source=/app/Data/cardscape.db
ENV Storage__LocalRoot=/app/Storage
EXPOSE 8080

ENTRYPOINT ["dotnet", "Cardscape.Api.dll"]
