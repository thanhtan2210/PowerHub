# syntax=docker/dockerfile:1
# Build context: repository root.
FROM mcr.microsoft.com/dotnet/sdk:10.0.401 AS build
WORKDIR /repo

COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/building-blocks/PowerHub.ServiceDefaults/PowerHub.ServiceDefaults.csproj src/building-blocks/PowerHub.ServiceDefaults/packages.lock.json src/building-blocks/PowerHub.ServiceDefaults/
COPY src/services/device/PowerHub.Device/PowerHub.Device.csproj src/services/device/PowerHub.Device/packages.lock.json src/services/device/PowerHub.Device/
RUN dotnet restore src/services/device/PowerHub.Device --locked-mode

COPY .editorconfig ./
COPY src/building-blocks/PowerHub.ServiceDefaults src/building-blocks/PowerHub.ServiceDefaults
COPY src/services/device/PowerHub.Device src/services/device/PowerHub.Device
RUN dotnet publish src/services/device/PowerHub.Device -c Release --no-restore -o /app \
    -p:OpenApiGenerateDocuments=false

# Chiseled image: no shell or package manager, runs as a non-root user by default.
FROM mcr.microsoft.com/dotnet/aspnet:10.0.12-noble-chiseled
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "PowerHub.Device.dll"]
