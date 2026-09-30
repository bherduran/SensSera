# Build context: repository root (docker build -f src/SensSera.Api/Dockerfile .)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first so the package layer is cached until a csproj changes.
COPY Directory.Build.props ./
COPY src/SensSera.Domain/SensSera.Domain.csproj src/SensSera.Domain/
COPY src/SensSera.Application/SensSera.Application.csproj src/SensSera.Application/
COPY src/SensSera.Infrastructure/SensSera.Infrastructure.csproj src/SensSera.Infrastructure/
COPY src/SensSera.Api/SensSera.Api.csproj src/SensSera.Api/
RUN dotnet restore src/SensSera.Api/SensSera.Api.csproj

COPY src/ src/
RUN dotnet publish src/SensSera.Api/SensSera.Api.csproj -c Release -o /app --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app ./
# Non-root user shipped with the official .NET images.
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "SensSera.Api.dll"]
