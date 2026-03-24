FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/SystemMonitor.AiService/SystemMonitor.AiService.csproj", "SystemMonitor.AiService/"]
COPY ["src/SystemMonitor.Shared/SystemMonitor.Shared.csproj",       "SystemMonitor.Shared/"]
RUN dotnet restore "SystemMonitor.AiService/SystemMonitor.AiService.csproj"

COPY src/SystemMonitor.AiService/ SystemMonitor.AiService/
COPY src/SystemMonitor.Shared/    SystemMonitor.Shared/
WORKDIR "/src/SystemMonitor.AiService"
RUN dotnet build   -c Release -o /app/build
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "SystemMonitor.AiService.dll"]
