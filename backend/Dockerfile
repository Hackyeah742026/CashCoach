# Build context: backend/
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY CashCoach.sln ./
COPY src/CashCoach.Core/CashCoach.Core.csproj src/CashCoach.Core/
COPY src/CashCoach.Infrastructure/CashCoach.Infrastructure.csproj src/CashCoach.Infrastructure/
COPY src/CashCoach.Api/CashCoach.Api.csproj src/CashCoach.Api/
RUN dotnet restore src/CashCoach.Api/CashCoach.Api.csproj
COPY src/ src/
RUN dotnet publish src/CashCoach.Api/CashCoach.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app .
# The image runs as the non-root `app` user, so the SQLite file lives in a directory it owns.
RUN mkdir -p /data && chown app:app /data
USER app
ENV ASPNETCORE_ENVIRONMENT=Production \
    ConnectionStrings__Default="Data Source=/data/cashcoach.db"
# Hosts like Render inject PORT; default to 8080 elsewhere.
CMD ["sh", "-c", "ASPNETCORE_HTTP_PORTS=${PORT:-8080} exec dotnet CashCoach.Api.dll"]
