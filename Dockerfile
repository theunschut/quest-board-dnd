# Use the official .NET 10 runtime as base image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

# Use the SDK image to build the app
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG VERSION=0.0.0-dev
WORKDIR /src

# Copy only production project files (exclude test projects)
COPY ["QuestBoard.Domain/QuestBoard.Domain.csproj", "QuestBoard.Domain/"]
COPY ["QuestBoard.Repository/QuestBoard.Repository.csproj", "QuestBoard.Repository/"]
COPY ["QuestBoard.Service/QuestBoard.Service.csproj", "QuestBoard.Service/"]

# Restore packages with BuildKit cache mount for faster rebuilds
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore "QuestBoard.Service/QuestBoard.Service.csproj"

# Copy source code for production projects only
COPY ["QuestBoard.Domain/", "QuestBoard.Domain/"]
COPY ["QuestBoard.Repository/", "QuestBoard.Repository/"]
COPY ["QuestBoard.Service/", "QuestBoard.Service/"]

# Build only the Service project (which transitively builds Domain and Repository)
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet build "QuestBoard.Service/QuestBoard.Service.csproj" -c Release --no-restore -p:Version=$VERSION

FROM build AS publish
ARG VERSION=0.0.0-dev
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish "QuestBoard.Service/QuestBoard.Service.csproj" -c Release -o /app/publish --no-build -p:Version=$VERSION

# Build runtime image
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "QuestBoard.Service.dll"]