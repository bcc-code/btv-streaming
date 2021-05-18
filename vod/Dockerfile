#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:5.0-buster-slim AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:5.0-buster-slim AS build
WORKDIR /src
COPY ["VODStreaming.csproj", ""]
RUN dotnet restore "./VODStreaming.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "VODStreaming.csproj" -c Release -o /app/build -r linux-x64

FROM build AS publish
RUN dotnet publish "VODStreaming.csproj" -c Release -o /app/publish -r linux-x64 -p:PublishReadyToRun=true

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "VODStreaming.dll"]
