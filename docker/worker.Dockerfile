FROM mcr.microsoft.com/dotnet/sdk:10.0.400 AS build
WORKDIR /src

COPY global.json Directory.Build.props Directory.Packages.props ./
COPY backend/src/StoreControl.Domain/StoreControl.Domain.csproj backend/src/StoreControl.Domain/
COPY backend/src/StoreControl.Application/StoreControl.Application.csproj backend/src/StoreControl.Application/
COPY backend/src/StoreControl.Infrastructure/StoreControl.Infrastructure.csproj backend/src/StoreControl.Infrastructure/
COPY backend/src/StoreControl.Worker/StoreControl.Worker.csproj backend/src/StoreControl.Worker/
RUN dotnet restore backend/src/StoreControl.Worker/StoreControl.Worker.csproj

COPY backend/src backend/src
RUN dotnet publish backend/src/StoreControl.Worker/StoreControl.Worker.csproj --configuration Release --no-restore --output /app/publish

FROM mcr.microsoft.com/dotnet/runtime:10.0.11 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "StoreControl.Worker.dll"]
