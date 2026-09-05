#!/bin/sh
# Runs `dotnet ef` inside the SDK container, since this workstation has no .NET SDK.
#
# Usage (from the repo root):
#   docker run --rm -v "${PWD}:/src" -w /src mcr.microsoft.com/dotnet/sdk:8.0 \
#     sh scripts/dotnet-ef.sh <project-dir> <command...>
#
# Example — add a migration to Stagiaire.Service:
#   ... sh scripts/dotnet-ef.sh Stagiaire.Service migrations add AddCandidatureFields
#
# Prefer this over hand-writing migrations: the generator emits the [Migration]/[DbContext]
# attributes and updates the model snapshot, both of which were missing from the original
# hand-written migrations and made EF silently discover nothing.
set -e

PROJECT_DIR="$1"
shift

export PATH="$PATH:/root/.dotnet/tools"
export DOTNET_CLI_TELEMETRY_OPTOUT=1

if ! command -v dotnet-ef >/dev/null 2>&1; then
  echo "--- installing dotnet-ef ---"
  dotnet tool install --global dotnet-ef --version 8.0.8 >/dev/null
fi

echo "--- dotnet ef $* (project: $PROJECT_DIR) ---"
dotnet ef "$@" \
  --project "$PROJECT_DIR/$PROJECT_DIR.csproj" \
  --startup-project "$PROJECT_DIR/$PROJECT_DIR.csproj"
