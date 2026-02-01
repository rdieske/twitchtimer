FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["TwitchStreamTimer.Web/TwitchStreamTimer.Web.csproj", "TwitchStreamTimer.Web/"]
RUN dotnet restore "TwitchStreamTimer.Web/TwitchStreamTimer.Web.csproj"
COPY . .
WORKDIR "/src/TwitchStreamTimer.Web"
RUN dotnet build "TwitchStreamTimer.Web.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "TwitchStreamTimer.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TwitchStreamTimer.Web.dll"]
