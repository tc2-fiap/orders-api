FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# git is needed only to bake this image's own commit SHA into
# build-info.json below — see notes.md for why that replaced the old
# --build-arg BUILD_SHA/BUILD_TIME dance (it was easy to forget to pass, or
# to pass computed against a not-yet-committed HEAD).
RUN apt-get update && apt-get install -y --no-install-recommends git && rm -rf /var/lib/apt/lists/*

COPY src/FiapGames.Orders.Api/FiapGames.Orders.Api.csproj src/FiapGames.Orders.Api/
RUN dotnet restore src/FiapGames.Orders.Api/FiapGames.Orders.Api.csproj

# The build context is this repo's own root now (not just the service
# folder) specifically so .git is present here for the next step.
COPY . .
RUN echo "{\"sha\":\"$(git rev-parse HEAD)\",\"buildTime\":\"$(date -u +%Y-%m-%dT%H:%M:%SZ)\"}" > /build-info.json

RUN dotnet publish src/FiapGames.Orders.Api/FiapGames.Orders.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

USER app

COPY --from=build /app/publish .
COPY --from=build /build-info.json .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "FiapGames.Orders.Api.dll"]
