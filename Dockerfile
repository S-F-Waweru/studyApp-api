# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# restore first (separate layer) so it is cached until a csproj changes
COPY StudyApp.Domain/StudyApp.Domain.csproj StudyApp.Domain/
COPY StudyApp.Application/StudyApp.Application.csproj StudyApp.Application/
COPY StudyApp.Infrastructure/StudyApp.Infrastructure.csproj StudyApp.Infrastructure/
COPY StudyApp.Api/StudyApp.Api.csproj StudyApp.Api/
RUN dotnet restore StudyApp.Api/StudyApp.Api.csproj

COPY . .
RUN dotnet publish StudyApp.Api/StudyApp.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

# ---------- runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

# uploaded documents/attachments (LocalFileStorage writes to <contentroot>/files);
# docker-compose mounts a volume/bind here so files survive container rebuilds
RUN mkdir -p /app/files

EXPOSE 8080
ENTRYPOINT ["dotnet", "StudyApp.Api.dll"]
