# syntax=docker/dockerfile:1.7
# Multi-stage build for the Cardscape API. Targets the .NET 10 LTS SDK
# and produces a self-contained ASP.NET Core image.

# ── Stage 1: restore + publish ──────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy build configuration first so the package restore layer caches.
COPY Directory.Build.props Directory.Packages.props global.json ./
COPY src/Cardscape.Domain/Cardscape.Domain.csproj           src/Cardscape.Domain/
COPY src/Cardscape.Application/Cardscape.Application.csproj src/Cardscape.Application/
COPY src/Cardscape.Infrastructure/Cardscape.Infrastructure.csproj src/Cardscape.Infrastructure/
COPY src/Cardscape.Web/Cardscape.Web.csproj                 src/Cardscape.Web/
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
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Install curl for the docker healthcheck. The .NET 10 aspnet image is
# debian-slim without curl; we add it here so the compose healthcheck
# can probe /health without spawning a second dotnet process (which
# would race the primary one for port 8080).
#
# BETA-8-ENV-#1 — see test-results/r8/r8-report.md. The previous
# `dotnet Cardscape.Api.dll --health` command tried to start a *second*
# Kestrel instance on :8080, which always exited with address-in-use
# and made the container report unhealthy forever even though the
# real API was answering 200 on /health.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl ca-certificates \
    && rm -rf /var/lib/apt/lists/*

# Non-root for safety; UID 1000 matches the .NET base image convention.
RUN groupadd --system --gid 1001 cardscape \
    && useradd  --system --uid 1001 --gid cardscape cardscape \
    && mkdir -p /app/Data /app/Storage \
    && chown -R cardscape:cardscape /app

COPY --from=build --chown=cardscape:cardscape /app/publish ./

# BETA-9-#5 — see test-results/r9/r9-report.md.
# The Blazor WebAssembly SDK generates the PWA service-worker files
# (service-worker.js, service-worker-assets.js + their .gz
# variants) into Cardscape.Web/bin/Release/net10.0/wwwroot/ as
# BUILD OUTPUTS of the <ServiceWorker> item. The dotnet publish
# step on the API does NOT copy them into /app/wwwroot/ — the
# static-web-assets manifest is generated before the Web's
# <ServiceWorker> target runs, so the runtime
# Cardscape.Api.staticwebassets.endpoints.json does not list the
# service-worker routes, and UseBlazorFrameworkFiles + UseStaticFiles
# therefore never serve /service-worker.js. The browser's PWA
# registration 404s, the rejected promise surfaces the Blazor
# "An unhandled error has occurred" overlay on every page load.
#
# Pull the four files out of the build stage and drop them into
# the runtime wwwroot. We tolerate an individual file being
# missing (the .gz variants are not produced in every config)
# by using `|| true` so a missing variant does not break the
# build. chown to the non-root cardscape user so the static-
# files middleware can read them.
COPY --from=build /src/src/Cardscape.Web/bin/Release/net10.0/wwwroot/service-worker.js /app/wwwroot/service-worker.js
COPY --from=build /src/src/Cardscape.Web/bin/Release/net10.0/wwwroot/service-worker-assets.js /app/wwwroot/service-worker-assets.js
COPY --from=build /src/src/Cardscape.Web/bin/Release/net10.0/wwwroot/service-worker.js.gz /app/wwwroot/service-worker.js.gz
COPY --from=build /src/src/Cardscape.Web/bin/Release/net10.0/wwwroot/service-worker-assets.js.gz /app/wwwroot/service-worker-assets.js.gz
RUN chown cardscape:cardscape /app/wwwroot/service-worker*.js /app/wwwroot/service-worker*.gz 2>/dev/null || true

USER cardscape

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV Database__Provider=Sqlite
ENV ConnectionStrings__Default=Data Source=/app/Data/cardscape.db
ENV Storage__LocalRoot=/app/Storage
EXPOSE 8080

ENTRYPOINT ["dotnet", "Cardscape.Api.dll"]
