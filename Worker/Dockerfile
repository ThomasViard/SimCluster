FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY Common/Common.csproj Common/Common.csproj
COPY Worker/Worker.csproj Worker/Worker.csproj

RUN dotnet restore Worker/Worker.csproj

COPY Common/ Common/
COPY Worker/ Worker/

RUN dotnet publish Worker/Worker.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Worker.dll"]
