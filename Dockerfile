# Build from repo root (API references Core + Infrastructure).
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY MetaAdsAnalyzer.sln ./
COPY MetaAdsAnalyzer.Core/MetaAdsAnalyzer.Core.csproj MetaAdsAnalyzer.Core/
COPY MetaAdsAnalyzer.Infrastructure/MetaAdsAnalyzer.Infrastructure.csproj MetaAdsAnalyzer.Infrastructure/
COPY MetaAdsAnalyzer.API/MetaAdsAnalyzer.API.csproj MetaAdsAnalyzer.API/

RUN dotnet restore MetaAdsAnalyzer.API/MetaAdsAnalyzer.API.csproj

COPY MetaAdsAnalyzer.Core/ MetaAdsAnalyzer.Core/
COPY MetaAdsAnalyzer.Infrastructure/ MetaAdsAnalyzer.Infrastructure/
COPY MetaAdsAnalyzer.API/ MetaAdsAnalyzer.API/

RUN dotnet publish MetaAdsAnalyzer.API/MetaAdsAnalyzer.API.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Render sets PORT; default 8080 for local.
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080

ENTRYPOINT ["/bin/sh", "-c", "exec dotnet MetaAdsAnalyzer.API.dll --urls \"http://0.0.0.0:${PORT:-8080}\""]
