# --- Build ---
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/StockFlow.Api/StockFlow.Api.csproj src/StockFlow.Api/
RUN dotnet restore src/StockFlow.Api/StockFlow.Api.csproj

COPY src/ src/
RUN dotnet publish src/StockFlow.Api/StockFlow.Api.csproj -c Release -o /app/publish --no-restore

# --- Runtime ---
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

RUN mkdir -p /data && chown app:app /data
COPY --from=build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080 \
    ConnectionStrings__Default="Data Source=/data/stockflow.db"

USER app
EXPOSE 8080
VOLUME ["/data"]

ENTRYPOINT ["dotnet", "StockFlow.Api.dll"]
