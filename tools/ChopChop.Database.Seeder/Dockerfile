FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore tools/ChopChop.Database.Seeder/ChopChop.Database.Seeder.csproj
RUN dotnet publish tools/ChopChop.Database.Seeder/ChopChop.Database.Seeder.csproj \
    --no-restore --configuration Release --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ChopChop.Database.Seeder.dll"]
