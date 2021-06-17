FROM mcr.microsoft.com/dotnet/aspnet:5.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build
WORKDIR /src
COPY ["btv-mediapackage-api/btv-mediapackage-api.csproj", "btv-mediapackage-api/"]
RUN dotnet restore "btv-mediapackage-api/btv-mediapackage-api.csproj"
COPY . .
WORKDIR "/src/btv-mediapackage-api"
RUN dotnet build "btv-mediapackage-api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "btv-mediapackage-api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "btv-mediapackage-api.dll"]
