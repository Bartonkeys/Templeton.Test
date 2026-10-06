# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/BestStories.Api/BestStories.Api.csproj", "src/BestStories.Api/"]
RUN dotnet restore "src/BestStories.Api/BestStories.Api.csproj"

COPY src/ src/
RUN dotnet publish "src/BestStories.Api/BestStories.Api.csproj" \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

USER $APP_UID
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "BestStories.Api.dll"]
