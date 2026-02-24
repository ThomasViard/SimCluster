FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY Dashboard/Dashboard.csproj Dashboard/Dashboard.csproj

RUN dotnet restore Dashboard/Dashboard.csproj

COPY Dashboard/ Dashboard/

RUN dotnet publish Dashboard/Dashboard.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "Dashboard.dll"]
