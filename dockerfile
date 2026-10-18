# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .

RUN dotnet --info
RUN ls -la
RUN ls -la Host || true

# Restore с подробным логом (это теперь главный источник диагностики)
RUN dotnet restore Host/Host.csproj -v diag

RUN dotnet publish Host/Host.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Host.dll"]