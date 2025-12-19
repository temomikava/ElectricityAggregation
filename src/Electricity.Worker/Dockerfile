# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
USER $APP_UID
WORKDIR /app


# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/Electricity.Worker/Electricity.Worker.csproj", "src/Electricity.Worker/"]
COPY ["src/Electricity.Infrastructure/Electricity.Infrastructure.csproj", "src/Electricity.Infrastructure/"]
COPY ["src/Electricity.Application/Electricity.Application.csproj", "src/Electricity.Application/"]
COPY ["src/Electricity.Domain/Electricity.Domain.csproj", "src/Electricity.Domain/"]
RUN dotnet restore "./src/Electricity.Worker/Electricity.Worker.csproj"
COPY . .
WORKDIR "/src/src/Electricity.Worker"
RUN dotnet build "./Electricity.Worker.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Electricity.Worker.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Electricity.Worker.dll"]