# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app
COPY Stagiaire.Contracts/Stagiaire.Contracts.csproj Stagiaire.Contracts/
COPY Smartek.Common/Smartek.Common.csproj Smartek.Common/
COPY Convention.Service/Convention.Service.csproj Convention.Service/
RUN dotnet restore Convention.Service/Convention.Service.csproj
COPY . .
WORKDIR /app/Convention.Service
RUN dotnet publish -c Release -o /out

# Stage 2: Run
FROM mcr.microsoft.com/dotnet/aspnet:8.0
# curl backs the Compose HEALTHCHECK on /health/live — not present in the aspnet runtime image.
#
# libfontconfig1 + a real font are required by QuestPDF: it renders through SkiaSharp, which needs
# native fontconfig to resolve a typeface. Without them generation still returns 200 and writes a
# structurally valid PDF, but one with no /Font resource — i.e. a blank page. It fails silently
# rather than throwing, so the only signal is a ~1 KB output file.
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        curl \
        libfontconfig1 \
        fonts-dejavu-core \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /out .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "Convention.Service.dll"]