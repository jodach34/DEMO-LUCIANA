FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY global.json Directory.Build.props ./
COPY src/Convalidacion.Web/Convalidacion.Web.csproj src/Convalidacion.Web/packages.lock.json ./src/Convalidacion.Web/
RUN dotnet restore src/Convalidacion.Web/Convalidacion.Web.csproj --locked-mode
COPY src/ ./src/
RUN dotnet publish src/Convalidacion.Web/Convalidacion.Web.csproj -c Release --no-restore -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Convalidacion.Web.dll"]
