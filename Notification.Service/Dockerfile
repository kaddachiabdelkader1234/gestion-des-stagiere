# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app
COPY Stagiaire.Contracts/Stagiaire.Contracts.csproj Stagiaire.Contracts/
COPY Smartek.Common/Smartek.Common.csproj Smartek.Common/
COPY Notification.Service/Notification.Service.csproj Notification.Service/
RUN dotnet restore Notification.Service/Notification.Service.csproj
COPY . .
WORKDIR /app/Notification.Service
RUN dotnet publish -c Release -o /out

# Stage 2: Run
FROM mcr.microsoft.com/dotnet/aspnet:8.0
# curl backs the Compose HEALTHCHECK on /health/live — not present in the aspnet runtime image.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /out .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "Notification.Service.dll"]