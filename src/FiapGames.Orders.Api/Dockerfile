FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY FiapGames.Orders.Api.csproj .
RUN dotnet restore FiapGames.Orders.Api.csproj

COPY . .

RUN dotnet publish FiapGames.Orders.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Not baked in automatically — the build context here is just this service's
# own project folder, with no .git to read from. Pass both at build time:
#   docker build --build-arg BUILD_SHA=$(git rev-parse HEAD) \
#                --build-arg BUILD_TIME=$(date -u +%Y-%m-%dT%H:%M:%SZ) ...
# Left as "unknown" otherwise, e.g. a plain `docker build` with neither arg.
ARG BUILD_SHA=unknown
ARG BUILD_TIME=unknown
ENV BUILD_SHA=$BUILD_SHA
ENV BUILD_TIME=$BUILD_TIME

USER app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "FiapGames.Orders.Api.dll"]
