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
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Cs2Admin.API.dll"]
