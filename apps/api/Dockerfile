# Stage 1: Build and publish
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

# Copy build configuration and Central Package Management files
COPY global.json Directory.Build.props Directory.Packages.props ./

# Copy API project files for dependency caching (DK-1)
COPY apps/api/src/SupportDesk.Domain/SupportDesk.Domain.csproj apps/api/src/SupportDesk.Domain/
COPY apps/api/src/SupportDesk.Application/SupportDesk.Application.csproj apps/api/src/SupportDesk.Application/
COPY apps/api/src/SupportDesk.Infrastructure/SupportDesk.Infrastructure.csproj apps/api/src/SupportDesk.Infrastructure/
COPY apps/api/src/SupportDesk.Presentation/SupportDesk.Presentation.csproj apps/api/src/SupportDesk.Presentation/

# Restore dependencies
RUN dotnet restore apps/api/src/SupportDesk.Presentation/SupportDesk.Presentation.csproj

# Copy source code and build/publish Release bundle
COPY apps/api/src/ apps/api/src/
RUN dotnet publish apps/api/src/SupportDesk.Presentation/SupportDesk.Presentation.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# Stage 2: Runtime environment
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Run as non-root user on port 8080 (DK-3)
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "SupportDesk.Presentation.dll"]
