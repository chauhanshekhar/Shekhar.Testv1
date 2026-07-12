FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["Trading.API/Trading.API.csproj", "Trading.API/"]
COPY ["Trading.Application/Trading.Application.csproj", "Trading.Application/"]
COPY ["Trading.Domain/Trading.Domain.csproj", "Trading.Domain/"]
COPY ["Trading.Infrastructure/Trading.Infrastructure.csproj", "Trading.Infrastructure/"]
COPY ["Trading.SignalEngine/Trading.SignalEngine.csproj", "Trading.SignalEngine/"]
RUN dotnet restore "Trading.API/Trading.API.csproj"

COPY . .
WORKDIR "/src/Trading.API"
RUN dotnet build "Trading.API.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Trading.API.csproj" -c $BUILD_CONFIGURATION --no-restore -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Trading.API.dll"]
