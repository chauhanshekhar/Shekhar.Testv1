FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["Trading.Worker/Trading.Worker.csproj", "Trading.Worker/"]
COPY ["Trading.Application/Trading.Application.csproj", "Trading.Application/"]
COPY ["Trading.Domain/Trading.Domain.csproj", "Trading.Domain/"]
COPY ["Trading.Infrastructure/Trading.Infrastructure.csproj", "Trading.Infrastructure/"]
COPY ["Trading.SignalEngine/Trading.SignalEngine.csproj", "Trading.SignalEngine/"]
RUN dotnet restore "Trading.Worker/Trading.Worker.csproj"
COPY . .
WORKDIR "/src/Trading.Worker"
RUN dotnet build "Trading.Worker.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Trading.Worker.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Trading.Worker.dll"]
