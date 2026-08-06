FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY EdhDeckBuilder.Core/EdhDeckBuilder.Core.csproj EdhDeckBuilder.Core/
COPY EdhDeckBuilder.Infrastructure/EdhDeckBuilder.Infrastructure.csproj EdhDeckBuilder.Infrastructure/
COPY EdhDeckBuilder.Agent/EdhDeckBuilder.Agent.csproj EdhDeckBuilder.Agent/
COPY EdhDeckBuilder.Web/EdhDeckBuilder.Web.csproj EdhDeckBuilder.Web/

RUN dotnet restore EdhDeckBuilder.Web/EdhDeckBuilder.Web.csproj

COPY . .

RUN dotnet publish EdhDeckBuilder.Web/EdhDeckBuilder.Web.csproj \
    --no-restore \
    --configuration Release \
    --output /app/publish

# Stable SDK (10.0.3xx) doesn't publish blazor.web.js; copy it from the repo
RUN mkdir -p /app/publish/wwwroot/_framework && \
    cp /src/docker/framework/blazor.web.js /app/publish/wwwroot/_framework/blazor.web.js

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_HTTP_PORTS=8080

EXPOSE 8080
ENTRYPOINT ["dotnet", "EdhDeckBuilder.Web.dll"]
