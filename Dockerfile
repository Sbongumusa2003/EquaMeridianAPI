# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY EquaMeridian.sln ./
COPY EquaMeridian/EquaMeridian.csproj EquaMeridian/
COPY EquaMeridian.Core/EquaMeridian.Core.csproj EquaMeridian.Core/
COPY EquaMeridian.Infrastructure/EquaMeridian.Infrastructure.csproj EquaMeridian.Infrastructure/

RUN dotnet restore

COPY . .

WORKDIR /src/EquaMeridian
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:10000
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish .

RUN mkdir -p /app/uploads

EXPOSE 10000
ENTRYPOINT ["dotnet", "EquaMeridian.dll"]
