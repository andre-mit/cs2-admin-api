FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["Cs2Admin.API.csproj", "./"]
RUN dotnet restore "Cs2Admin.API.csproj"
COPY . .
WORKDIR "/src/"
RUN dotnet build "Cs2Admin.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Cs2Admin.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*
COPY --from=publish /app/publish .

HEALTHCHECK --interval=30s --timeout=3s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "Cs2Admin.API.dll"]
