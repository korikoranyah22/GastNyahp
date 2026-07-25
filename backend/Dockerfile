FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/GastNyahp.Domain/GastNyahp.Domain.csproj ./src/GastNyahp.Domain/
COPY src/GastNyahp.Infrastructure/GastNyahp.Infrastructure.csproj ./src/GastNyahp.Infrastructure/
COPY src/GastNyahp.Api/GastNyahp.Api.csproj ./src/GastNyahp.Api/
RUN dotnet restore src/GastNyahp.Api/GastNyahp.Api.csproj

COPY src/ ./src/
RUN dotnet publish src/GastNyahp.Api/GastNyahp.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV PORT=5050 \
    ASPNETCORE_HTTP_PORTS=5050 \
    ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_URLS=http://[::]:5050
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && useradd --no-create-home --shell /bin/false appuser
COPY --from=build /app/publish ./
USER appuser
EXPOSE 5050
ENTRYPOINT ["dotnet", "GastNyahp.Api.dll"]
