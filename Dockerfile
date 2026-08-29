# 1. Build Stage (Includes .NET SDK and Node.js)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Install Node.js (v18) and npm required for the Angular build
RUN apt-get update && apt-get install -y curl gnupg \
    && curl -fsSL https://deb.nodesource.com/setup_18.x | bash - \
    && apt-get install -y nodejs \
    && rm -rf /var/lib/apt/lists/*

# Copy all source files
COPY . .

# Restore .NET dependencies
RUN dotnet restore "AllenKerberAutoSupply.csproj"

# Build and publish both the .NET API and the Angular frontend
RUN dotnet publish "AllenKerberAutoSupply.csproj" -c Release -o /app/publish

# 2. Runtime Stage (Lightweight production image, Node.js is NOT needed here)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Copy the compiled output from the build stage
COPY --from=build /app/publish .

# Expose port 8080
EXPOSE 8080

# Configure ASP.NET to bind to port 8080
ENV ASPNETCORE_URLS=http://+:8080

# Start the web application
ENTRYPOINT ["dotnet", "AllenKerberAutoSupply.dll"]
