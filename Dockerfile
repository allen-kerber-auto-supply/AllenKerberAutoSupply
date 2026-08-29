# 1. Build and Publish Stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy all source files and restore dependencies
COPY . .
RUN dotnet restore "AllenKerberAutoSupply.csproj"

# Build and publish the release build
RUN dotnet publish "AllenKerberAutoSupply.csproj" -c Release -o /app/publish

# 2. Runtime Stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Copy the published output from the build stage
COPY --from=build /app/publish .

# Expose the port defined by the environment variable
EXPOSE 8080

# Configure ASP.NET to bind to port 8080
ENV ASPNETCORE_URLS=http://+:8080

# Start the web application
ENTRYPOINT ["dotnet", "AllenKerberAutoSupply.dll"]
