FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/SystemMonitor.DataSeeder/SystemMonitor.DataSeeder.csproj", "SystemMonitor.DataSeeder/"]
COPY ["src/SystemMonitor.Agent/SystemMonitor.Agent.csproj",           "SystemMonitor.Agent/"]
COPY ["src/SystemMonitor.Shared/SystemMonitor.Shared.csproj",         "SystemMonitor.Shared/"]
RUN dotnet restore "SystemMonitor.DataSeeder/SystemMonitor.DataSeeder.csproj"

COPY src/SystemMonitor.DataSeeder/ SystemMonitor.DataSeeder/
COPY src/SystemMonitor.Agent/      SystemMonitor.Agent/
COPY src/SystemMonitor.Shared/     SystemMonitor.Shared/
WORKDIR "/src/SystemMonitor.DataSeeder"
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "SystemMonitor.DataSeeder.dll"]
