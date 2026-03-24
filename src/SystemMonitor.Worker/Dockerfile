FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/SystemMonitor.Worker/SystemMonitor.Worker.csproj", "SystemMonitor.Worker/"]
COPY ["src/SystemMonitor.Shared/SystemMonitor.Shared.csproj", "SystemMonitor.Shared/"]
RUN dotnet restore "SystemMonitor.Worker/SystemMonitor.Worker.csproj"

COPY src/SystemMonitor.Worker/ SystemMonitor.Worker/
COPY src/SystemMonitor.Shared/ SystemMonitor.Shared/
WORKDIR "/src/SystemMonitor.Worker"
RUN dotnet build   -c Release -o /app/build
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "SystemMonitor.Worker.dll"]
