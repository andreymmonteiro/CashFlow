# Use ASP.NET runtime as base for final image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy csproj and restore as distinct layers
COPY TransactionService.csproj ./
RUN dotnet restore TransactionService.csproj

# Copy everything else and build
COPY . ./
RUN dotnet build TransactionService.csproj -c $BUILD_CONFIGURATION -o /app/build

# Publish stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish TransactionService.csproj -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Final stage
FROM base AS final
WORKDIR /app

# Optional: if you really want to use a non-root user, define it here:
# ENV APP_UID=1000
# RUN useradd -u $APP_UID appuser && chown -R appuser /app
# USER appuser

COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "TransactionService.dll"]
