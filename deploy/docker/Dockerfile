FROM mcr.microsoft.com/dotnet/sdk:10.0 AS restore
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/Lewdventure.Server.Contracts/Lewdventure.Server.Contracts.csproj src/Lewdventure.Server.Contracts/packages.lock.json src/Lewdventure.Server.Contracts/
COPY src/Lewdventure.Server.GameConfig/Lewdventure.Server.GameConfig.csproj src/Lewdventure.Server.GameConfig/packages.lock.json src/Lewdventure.Server.GameConfig/
COPY src/Lewdventure.Server.Battle/Lewdventure.Server.Battle.csproj src/Lewdventure.Server.Battle/packages.lock.json src/Lewdventure.Server.Battle/
COPY src/Lewdventure.Server.Infrastructure/Lewdventure.Server.Infrastructure.csproj src/Lewdventure.Server.Infrastructure/packages.lock.json src/Lewdventure.Server.Infrastructure/
COPY src/Lewdventure.Server.Runs/Lewdventure.Server.Runs.csproj src/Lewdventure.Server.Runs/packages.lock.json src/Lewdventure.Server.Runs/
COPY src/Lewdventure.Server.Api/Lewdventure.Server.Api.csproj src/Lewdventure.Server.Api/packages.lock.json src/Lewdventure.Server.Api/
COPY src/Lewdventure.Server.Admin/Lewdventure.Server.Admin.csproj src/Lewdventure.Server.Admin/packages.lock.json src/Lewdventure.Server.Admin/
COPY tools/Lewdventure.Server.ConfigTool/Lewdventure.Server.ConfigTool.csproj tools/Lewdventure.Server.ConfigTool/packages.lock.json tools/Lewdventure.Server.ConfigTool/
RUN dotnet restore src/Lewdventure.Server.Api/Lewdventure.Server.Api.csproj --locked-mode \
 && dotnet restore tools/Lewdventure.Server.ConfigTool/Lewdventure.Server.ConfigTool.csproj --locked-mode \
 && dotnet restore src/Lewdventure.Server.Admin/Lewdventure.Server.Admin.csproj --locked-mode

FROM restore AS build
ARG SOURCE_REVISION=unknown
COPY src/ src/
COPY tools/ tools/
RUN dotnet publish src/Lewdventure.Server.Api/Lewdventure.Server.Api.csproj -c Release -o /out/api --no-restore /p:UseAppHost=false /p:SourceRevisionId=${SOURCE_REVISION} \
 && dotnet publish tools/Lewdventure.Server.ConfigTool/Lewdventure.Server.ConfigTool.csproj -c Release -o /out/tool --no-restore /p:UseAppHost=false /p:SourceRevisionId=${SOURCE_REVISION} \
 && dotnet publish src/Lewdventure.Server.Admin/Lewdventure.Server.Admin.csproj -c Release -o /out/admin --no-restore /p:UseAppHost=false /p:SourceRevisionId=${SOURCE_REVISION} \
 && mkdir -p /out/api/cache /out/admin/data

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra AS api
WORKDIR /app
ENV ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_gcServer=1 \
    TZ=UTC
COPY --from=build --chown=$APP_UID:$APP_UID /out/api .
USER $APP_UID
EXPOSE 5000 9090
HEALTHCHECK --interval=30s --timeout=5s --start-period=60s --retries=3 CMD ["dotnet", "Lewdventure.Server.Api.dll", "--health-probe", "http://127.0.0.1:9090/health/live"]
ENTRYPOINT ["dotnet", "Lewdventure.Server.Api.dll"]

FROM mcr.microsoft.com/dotnet/runtime:10.0-noble-chiseled-extra AS config-tool
WORKDIR /app
ENV DOTNET_ENVIRONMENT=Production \
    TZ=UTC
COPY --from=build --chown=$APP_UID:$APP_UID /out/tool .
USER $APP_UID
ENTRYPOINT ["dotnet", "Lewdventure.Server.ConfigTool.dll"]

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra AS admin
WORKDIR /app
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    AdminPanel__DataPath=/app/data \
    TZ=UTC
COPY --from=build --chown=$APP_UID:$APP_UID /out/admin .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Lewdventure.Server.Admin.dll"]
