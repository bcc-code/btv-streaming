
FROM mcr.microsoft.com/dotnet/aspnet:5.0-buster-slim AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:5.0-buster-slim AS build
WORKDIR /src
COPY ["MediaPackageAPI.csproj", ""]
RUN dotnet restore "./MediaPackageAPI.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "MediaPackageAPI.csproj" -c Release -o /app/build -r linux-x64

FROM build AS publish
RUN dotnet publish "MediaPackageAPI.csproj" -c Release -o /app/publish -r linux-x64 -p:PublishReadyToRun=true

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MediaPackageAPI.dll"]