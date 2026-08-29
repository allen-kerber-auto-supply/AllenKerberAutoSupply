FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Install Node.js (v18 or change to your required version)
RUN apt-get update && apt-get install -y curl \
    && curl -sL https://deb.nodesource.com/setup_18.x | bash - \
    && apt-get install -y nodejs

# Copy and restore/publish as usual
COPY . .
RUN dotnet restore "AllenKerberAutoSupply.csproj"
RUN dotnet publish "AllenKerberAutoSupply.csproj" -c Release -o /app/publish
