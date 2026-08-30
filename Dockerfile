# 1. Build Stage (Includes .NET SDK and the latest Node.js)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Install the latest Node.js version and npm
RUN apt-get update && apt-get install -y curl gnupg \
    && curl -fsSL https://deb.nodesource.com/setup_current.x | bash - \
    && apt-get install -y nodejs \
    && rm -rf /var/lib/apt/lists/*

# Copy all source files
COPY . .

# Restore .NET dependencies
RUN dotnet restore "AllenKerberAutoSupply.csproj"

# Build and publish both the .NET API and the Angular frontend
RUN dotnet publish "AllenKerberAutoSupply.csproj" -c Release -o /app/publish

# Step 6: Copy compiled assets, accounting for Angular 17+'s nested /browser/ folder
RUN mkdir -p /app/publish/ClientApp/dist && \
    (cp -r /src/ClientApp/dist/browser/* /app/publish/ClientApp/dist/ 2>/dev/null || \
     cp -r /src/ClientApp/dist/* /app/publish/ClientApp/dist/)

# 2. Runtime Stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Copy the compiled output from the build stage
COPY --from=build /app/publish .

# Expose port 4595
EXPOSE 8080

# Configure ASP.NET to bind to port 4595
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "AllenKerberAutoSupply.dll"]
