# The API image. Build from the repository root:
#   docker build -f apps/api/Dockerfile .
# Nothing environment-specific is baked in: the connection string, JWT key and environment name
# are passed in when the container starts (see docker-compose.yml and .env.example).

# --- Build: the full .NET SDK, only in this stage --------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, from just the files that decide which packages are needed. This layer is only
# rebuilt when one of them changes, so editing a .cs file reuses the cached restore.
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY apps/api/src/SupportDesk.Domain/SupportDesk.Domain.csproj apps/api/src/SupportDesk.Domain/
COPY apps/api/src/SupportDesk.Application/SupportDesk.Application.csproj apps/api/src/SupportDesk.Application/
COPY apps/api/src/SupportDesk.Infrastructure/SupportDesk.Infrastructure.csproj apps/api/src/SupportDesk.Infrastructure/
COPY apps/api/src/SupportDesk.Presentation/SupportDesk.Presentation.csproj apps/api/src/SupportDesk.Presentation/
RUN dotnet restore apps/api/src/SupportDesk.Presentation/SupportDesk.Presentation.csproj

# Then the source, and publish without restoring again.
COPY .editorconfig ./
COPY apps/api/src/ apps/api/src/
RUN dotnet publish apps/api/src/SupportDesk.Presentation/SupportDesk.Presentation.csproj \
    --configuration Release --no-restore --output /app/publish

# --- Run: the much smaller ASP.NET runtime, no SDK, no source --------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Plain HTTP on 8080 (no certificates in the image), as the image's built-in non-root user.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID

ENTRYPOINT ["dotnet", "SupportDesk.Presentation.dll"]
