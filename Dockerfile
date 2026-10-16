# syntax=docker/dockerfile:1

# ---- Build stage -----------------------------------------------------------
# The SDK image needs network access: the build downloads the pinned Tailwind CLI
# (Tailwind.targets picks the linux-x64 or linux-arm64 binary and checks its SHA-256).
# The tags 10.0 float on purpose (security patches); global.json requires SDK 10.0.401 or newer, so a build against an older
# SDK fails loudly instead of silently. Pin a patch tag or digest here if reproducible images become a requirement.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, with only the files that decide the dependency graph, so the layer is cached until a package changes.
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/RutaProducto.Domain/RutaProducto.Domain.csproj src/RutaProducto.Domain/
COPY src/RutaProducto.Application/RutaProducto.Application.csproj src/RutaProducto.Application/
COPY src/RutaProducto.Infrastructure/RutaProducto.Infrastructure.csproj src/RutaProducto.Infrastructure/
COPY src/RutaProducto.Web/RutaProducto.Web.csproj src/RutaProducto.Web/
RUN dotnet restore src/RutaProducto.Web/RutaProducto.Web.csproj

COPY src/ src/
RUN dotnet publish src/RutaProducto.Web/RutaProducto.Web.csproj \
    --configuration Release --no-restore --output /app/publish

# ---- Runtime stage ---------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Relative paths in the configuration (Logs/, App_Data/attachments) resolve against /app, so both must be writable
# by the non-root user. On Railway the disk is ephemeral: attachments are lost on every deploy until S3 exists.
RUN mkdir -p /app/App_Data /app/Logs && chown -R app:app /app/App_Data /app/Logs

COPY --from=build --chown=app:app /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production

# The aspnet image already defines the unprivileged "app" user. Railway injects PORT; without it the image listens on 8080.
USER app
# Documentation only: the app listens on $PORT when Railway sets it (the base image's HTTP_PORTS=8080 is then overridden, which
# Kestrel reports as a warning). Railway checks /health through its own settings, so there is no HEALTHCHECK instruction.
EXPOSE 8080

ENTRYPOINT ["dotnet", "RutaProducto.Web.dll"]
