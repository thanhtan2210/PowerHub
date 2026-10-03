# syntax=docker/dockerfile:1
# Build context: repository root.
FROM mcr.microsoft.com/dotnet/sdk:10.0.401 AS build
WORKDIR /repo

COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/building-blocks/PowerHub.ServiceDefaults/PowerHub.ServiceDefaults.csproj src/building-blocks/PowerHub.ServiceDefaults/packages.lock.json src/building-blocks/PowerHub.ServiceDefaults/
COPY src/services/identity/PowerHub.Identity/PowerHub.Identity.csproj src/services/identity/PowerHub.Identity/packages.lock.json src/services/identity/PowerHub.Identity/
RUN dotnet restore src/services/identity/PowerHub.Identity --locked-mode

COPY .editorconfig ./
COPY src/building-blocks/PowerHub.ServiceDefaults src/building-blocks/PowerHub.ServiceDefaults
COPY src/services/identity/PowerHub.Identity src/services/identity/PowerHub.Identity
RUN dotnet publish src/services/identity/PowerHub.Identity -c Release --no-restore -o /app \
    -p:OpenApiGenerateDocuments=false

# Chiseled image: no shell or package manager, runs as a non-root user by default.
FROM mcr.microsoft.com/dotnet/aspnet:10.0.12-noble-chiseled
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "PowerHub.Identity.dll"]
