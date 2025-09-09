# Multi-stage Dockerfile for DataValidatorApi (.NET 8)

# Base runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
# Expose HTTP port
EXPOSE 8080
# Configure Kestrel to listen on port 8080
ENV ASPNETCORE_URLS=http://+:8080
# Default environment (override per-run)
ENV ASPNETCORE_ENVIRONMENT=Production

# Build image
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
ARG BUILD_CONFIGURATION=Release
# Copy SDK resolver first to honor required SDK version
COPY ["global.json", "." ]
# Copy csproj and restore as distinct layers
COPY ["DataValidatorApi.csproj", "." ]
RUN dotnet restore "./DataValidatorApi.csproj"
# Copy the rest of the source
COPY . .
# Publish (no restore, uses build configuration ARG)
RUN dotnet publish "DataValidatorApi.csproj" -c ${BUILD_CONFIGURATION} --no-restore -o /app/publish /p:UseAppHost=false

# Final image
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
# Run as non-root user provided by the base image (for .NET 8 images this is 'app')
USER app
# Note: pass API keys at runtime, e.g. -e OPENAI_API_KEY=... -e GOOGLE_API_KEY=...
ENTRYPOINT ["dotnet", "DataValidatorApi.dll"]