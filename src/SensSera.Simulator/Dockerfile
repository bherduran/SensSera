# Build context: repository root (docker build -f src/SensSera.Simulator/Dockerfile .)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props ./
COPY src/SensSera.Simulator/SensSera.Simulator.csproj src/SensSera.Simulator/
RUN dotnet restore src/SensSera.Simulator/SensSera.Simulator.csproj
COPY src/SensSera.Simulator/ src/SensSera.Simulator/
RUN dotnet publish src/SensSera.Simulator/SensSera.Simulator.csproj -c Release -o /app --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app
COPY --from=build /app ./
USER $APP_UID
ENTRYPOINT ["dotnet", "SensSera.Simulator.dll"]
