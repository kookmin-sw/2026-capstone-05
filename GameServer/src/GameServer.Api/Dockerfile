# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/GameServer.Api/GameServer.Api.csproj", "src/GameServer.Api/"]
COPY ["src/GameServer.Application/GameServer.Application.csproj", "src/GameServer.Application/"]
COPY ["src/GameServer.Infrastructure/GameServer.Infrastructure.csproj", "src/GameServer.Infrastructure/"]
COPY ["src/GameServer.Domain/GameServer.Domain.csproj", "src/GameServer.Domain/"]
RUN dotnet restore "src/GameServer.Api/GameServer.Api.csproj"

COPY . .
WORKDIR "/src/src/GameServer.Api"
RUN dotnet publish "GameServer.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "GameServer.Api.dll"]
