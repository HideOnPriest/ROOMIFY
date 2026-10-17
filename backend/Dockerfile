# Osobne kopiowanie metadanych pozwala wykorzystać cache przywracania NuGet.
FROM mcr.microsoft.com/dotnet/sdk:10.0.301-noble AS build
WORKDIR /src
COPY src/Roomify.Api/Roomify.Api.csproj src/Roomify.Api/
COPY src/Roomify.Application/Roomify.Application.csproj src/Roomify.Application/
COPY src/Roomify.Domain/Roomify.Domain.csproj src/Roomify.Domain/
COPY src/Roomify.Infrastructure/Roomify.Infrastructure.csproj src/Roomify.Infrastructure/
# Rozwiązanie zawiera także testy; obraz aplikacji buduje tylko graf zależności API.
RUN dotnet restore src/Roomify.Api/Roomify.Api.csproj
COPY src/ src/
RUN dotnet build src/Roomify.Api/Roomify.Api.csproj --configuration Release --no-restore

FROM build AS publish
RUN dotnet publish src/Roomify.Api/Roomify.Api.csproj \
    --configuration Release --no-build --output /app/publish /p:UseAppHost=false

# Noble zawiera Bash: healthcheck nie wymaga dodatkowych pakietów sieciowych.
FROM mcr.microsoft.com/dotnet/aspnet:10.0.9-noble AS runtime
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
COPY --from=publish --chown=app:app /app/publish ./
COPY --chown=app:app --chmod=0555 healthcheck.sh /app/healthcheck.sh
USER $APP_UID
ENTRYPOINT ["dotnet", "Roomify.Api.dll"]
