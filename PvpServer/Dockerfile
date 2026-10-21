# Ball Battle PvP server for Render (free web service). Build context = repository root (see render.yaml).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY BallBattleUnity/Assets/Scripts/Sim BallBattleUnity/Assets/Scripts/Sim
COPY PvpServer/PvpServer.csproj PvpServer/
RUN dotnet restore PvpServer/PvpServer.csproj
COPY PvpServer/*.cs PvpServer/
RUN dotnet publish PvpServer/PvpServer.csproj -c Release -o /app --no-restore -p:InvariantGlobalization=true

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_ENVIRONMENT=Production DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1
# Render sets PORT (10000 by default); Program.cs listens on it.
EXPOSE 10000
ENTRYPOINT ["dotnet", "PvpServer.dll"]
