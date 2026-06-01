# ── Stage 1: Build ────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy and restore dependencies first (layer caching)
COPY code/SecureTrace.API/SecureTrace.API.csproj ./
RUN dotnet restore

# Copy everything else and publish
COPY code/SecureTrace.API/ ./
RUN dotnet publish -c Release -o /app/publish

# ── Stage 2: Runtime ──────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

# Render sets PORT environment variable automatically
ENV ASPNETCORE_URLS=http://+:${PORT:-10000}

ENTRYPOINT ["dotnet", "SecureTrace.API.dll"]