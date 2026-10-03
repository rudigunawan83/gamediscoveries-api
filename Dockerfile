FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY GameDiscoveries.sln ./
COPY src/GameDiscoveries.Api/GameDiscoveries.Api.csproj src/GameDiscoveries.Api/
COPY src/GameDiscoveries.BuildingBlocks/GameDiscoveries.BuildingBlocks.csproj src/GameDiscoveries.BuildingBlocks/
COPY src/GameDiscoveries.Infrastructure/GameDiscoveries.Infrastructure.csproj src/GameDiscoveries.Infrastructure/
COPY src/GameDiscoveries.Modules.Catalog/GameDiscoveries.Modules.Catalog.csproj src/GameDiscoveries.Modules.Catalog/
COPY src/GameDiscoveries.Modules.Providers/GameDiscoveries.Modules.Providers.csproj src/GameDiscoveries.Modules.Providers/
COPY src/GameDiscoveries.Modules.Discovery/GameDiscoveries.Modules.Discovery.csproj src/GameDiscoveries.Modules.Discovery/
COPY src/GameDiscoveries.Modules.Recommendation/GameDiscoveries.Modules.Recommendation.csproj src/GameDiscoveries.Modules.Recommendation/
COPY src/GameDiscoveries.Modules.Search/GameDiscoveries.Modules.Search.csproj src/GameDiscoveries.Modules.Search/
COPY src/GameDiscoveries.Modules.Users/GameDiscoveries.Modules.Users.csproj src/GameDiscoveries.Modules.Users/
COPY src/GameDiscoveries.Modules.Favorites/GameDiscoveries.Modules.Favorites.csproj src/GameDiscoveries.Modules.Favorites/
COPY src/GameDiscoveries.Modules.Analytics/GameDiscoveries.Modules.Analytics.csproj src/GameDiscoveries.Modules.Analytics/
COPY src/GameDiscoveries.Modules.Developer/GameDiscoveries.Modules.Developer.csproj src/GameDiscoveries.Modules.Developer/
COPY src/GameDiscoveries.Modules.Advertising/GameDiscoveries.Modules.Advertising.csproj src/GameDiscoveries.Modules.Advertising/
COPY src/GameDiscoveries.Modules.Administration/GameDiscoveries.Modules.Administration.csproj src/GameDiscoveries.Modules.Administration/

RUN dotnet restore src/GameDiscoveries.Api/GameDiscoveries.Api.csproj

COPY src/ ./src/
RUN dotnet publish src/GameDiscoveries.Api/GameDiscoveries.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "GameDiscoveries.Api.dll"]
