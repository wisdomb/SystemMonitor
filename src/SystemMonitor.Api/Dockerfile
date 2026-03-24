FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/SystemMonitor.Api/SystemMonitor.Api.csproj",    "SystemMonitor.Api/"]
COPY ["src/SystemMonitor.Shared/SystemMonitor.Shared.csproj", "SystemMonitor.Shared/"]
RUN dotnet restore "SystemMonitor.Api/SystemMonitor.Api.csproj"

COPY src/SystemMonitor.Api/     SystemMonitor.Api/
COPY src/SystemMonitor.Shared/  SystemMonitor.Shared/
WORKDIR "/src/SystemMonitor.Api"
RUN dotnet build  -c Release -o /app/build
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "SystemMonitor.Api.dll"]
