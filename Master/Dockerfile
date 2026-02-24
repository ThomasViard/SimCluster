FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY Common/Common.csproj Common/Common.csproj
COPY Master/Master.csproj Master/Master.csproj

RUN dotnet restore Master/Master.csproj

COPY Common/ Common/
COPY Master/ Master/

RUN dotnet publish Master/Master.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "Master.dll"]
