#!/usr/bin/env sh
# Runs the .NET SDK in a container so no local SDK is required.
# Usage: scripts/dotnet.sh build | scripts/dotnet.sh test | scripts/dotnet.sh ef ...
set -eu
# pwd -W yields a Windows path under Git Bash, which Docker Desktop needs for the bind mount.
root="$(cd "$(dirname "$0")/.." && (pwd -W 2>/dev/null || pwd))"
MSYS_NO_PATHCONV=1 exec docker run --rm \
  --network "${POWERHUB_DOCKER_NETWORK:-bridge}" \
  -e POWERHUB_TEST_POSTGRES \
  -e DOTNET_CLI_TELEMETRY_OPTOUT=1 \
  -v powerhub-nuget:/root/.nuget/packages \
  -v "$root:/repo" -w /repo \
  mcr.microsoft.com/dotnet/sdk:10.0 dotnet "$@"
