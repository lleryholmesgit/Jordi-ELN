# syntax=docker/dockerfile:1.7

FROM mcr.microsoft.com/dotnet/sdk:10.0.202-noble AS build
WORKDIR /src

COPY NuGet.Config ./
COPY ElectronicLabNotebook.csproj ./
RUN dotnet restore ElectronicLabNotebook.csproj --configfile NuGet.Config

COPY . .
RUN dotnet publish ElectronicLabNotebook.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0.6-noble AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true

RUN groupadd --system --gid 10001 appgroup \
    && useradd --system --uid 10001 --gid appgroup --home-dir /home/appuser --create-home --shell /usr/sbin/nologin appuser \
    && mkdir -p /app/App_Data/Uploads /app/App_Data/DataProtectionKeys /app/App_Data/OutgoingEmail \
    && chown -R 10001:10001 /app /home/appuser

COPY --from=build --chown=10001:10001 /app/publish .

USER 10001:10001
EXPOSE 8080

ENTRYPOINT ["dotnet", "ElectronicLabNotebook.dll"]
