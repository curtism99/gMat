# Stage 1: The 'build' stage - uses the full SDK to build the application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy the .csproj file first and restore packages.
# This is a key optimization. It copies the .csproj from the build context's root
# into the container's /src directory.
COPY ["gMat.Api.csproj", "."]
RUN dotnet restore "./gMat.Api.csproj"

# Copy the rest of the source code
COPY . .
RUN dotnet build "gMat.Api.csproj" -c Release -o /app/build

# Stage 2: The 'publish' stage
FROM build AS publish
RUN dotnet publish "gMat.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 3: The 'final' stage - uses the lightweight runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Install ffmpeg
USER root
RUN apt-get update && apt-get install -y ffmpeg && rm -rf /var/lib/apt/lists/*
USER app # Switch back to the non-root user for better security

# Set the port Kestrel will listen on
ENV ASPNETCORE_URLS=http://+:80

# Copy the published application from the 'publish' stage
COPY --from=publish /app/publish .

# Set the entrypoint for the container
ENTRYPOINT ["dotnet", "gMat.Api.dll"]