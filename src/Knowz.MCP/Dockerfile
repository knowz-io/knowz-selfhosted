# Base stage: ASP.NET 9.0 runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app

# Build stage: .NET SDK for compilation
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /source

# Copy csproj files and restore dependencies (layer caching)
COPY ["src/Knowz.MCP/Knowz.MCP.csproj", "src/Knowz.MCP/"]
RUN dotnet restore "src/Knowz.MCP/Knowz.MCP.csproj"

# Copy all source code and publish
COPY . .
WORKDIR /source/src/Knowz.MCP
RUN dotnet publish -c Release -o /app/publish --no-restore /p:UseAppHost=false

# Final stage: Minimal runtime image
FROM base AS final
WORKDIR /app

# Copy published output
COPY --from=build /app/publish .

# Set ASP.NET Core environment variables
ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_EnableDiagnostics=0

# Create non-root user for security (consistent with API/Worker)
RUN adduser --disabled-password --gecos '' appuser && \
    chown -R appuser:appuser /app

# Switch to non-root user
USER appuser

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=10s --start-period=15s --retries=3 \
  CMD curl -f http://localhost:8080/.well-known/oauth-authorization-server || exit 1

ENTRYPOINT ["dotnet", "Knowz.MCP.dll"]
