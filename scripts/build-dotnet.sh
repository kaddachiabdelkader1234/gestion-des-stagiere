#!/bin/sh
# Builds every .NET project in this repo inside the SDK container.
# Used because this workstation has only the .NET runtime installed, not the SDK.
set -e

for proj in \
  Smartek.Common/Smartek.Common.csproj \
  Stagiaire.Contracts/Stagiaire.Contracts.csproj \
  Stagiaire.Service/Stagiaire.Service.csproj \
  Convention.Service/Convention.Service.csproj \
  Evaluation.Service/Evaluation.Service.csproj \
  Notification.Service/Notification.Service.csproj
do
  echo "=== $proj ==="
  dotnet build "$proj" --nologo -v q
done

echo "ALL BUILDS OK"
