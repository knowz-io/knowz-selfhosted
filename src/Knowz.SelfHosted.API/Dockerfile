# Base stage: ASP.NET 10.0 runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app

# Build stage: .NET SDK for compilation
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

# Copy NuGet config for restore
COPY ["nuget.config", "."]

# Copy project files for NuGet restore caching
COPY ["src/Knowz.SelfHosted.Infrastructure/Knowz.SelfHosted.Infrastructure.csproj", "src/Knowz.SelfHosted.Infrastructure/"]
COPY ["src/Knowz.SelfHosted.Application/Knowz.SelfHosted.Application.csproj", "src/Knowz.SelfHosted.Application/"]
COPY ["src/Knowz.SelfHosted.API/Knowz.SelfHosted.API.csproj", "src/Knowz.SelfHosted.API/"]

# Restore NuGet packages
RUN dotnet restore "src/Knowz.SelfHosted.API/Knowz.SelfHosted.API.csproj"

# Copy all source code and publish
COPY . .
WORKDIR "/source/src/Knowz.SelfHosted.API"
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

# Create local file storage directory writable by non-root user
RUN mkdir -p /data/files && chown -R $APP_UID:$APP_UID /data/files

# Switch to built-in non-root user (APP_UID=1654 in .NET 10 base image)
USER $APP_UID

EXPOSE 8080

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=30s --retries=3 \
    CMD curl -f http://localhost:8080/healthz || exit 1

ENTRYPOINT ["dotnet", "Knowz.SelfHosted.API.dll"]
