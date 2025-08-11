#!/bin/bash

echo "======================================"
echo "ASP.NET WebAPI on Mac - Quick Start"
echo "======================================"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Function to check if a command exists
command_exists() {
    command -v "$1" >/dev/null 2>&1
}

# Check for .NET SDK
if command_exists dotnet; then
    echo -e "${GREEN}✓ .NET SDK found${NC}"
    dotnet --version
else
    echo -e "${YELLOW}⚠ .NET SDK not found. Installing...${NC}"
    # Install .NET 6 SDK for Mac
    curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --channel 6.0
    export PATH="$HOME/.dotnet:$PATH"
fi

# Navigate to the Core project directory
cd Custom.Gravitate.WebAPI.Core

# Restore packages
echo -e "${YELLOW}Restoring NuGet packages...${NC}"
dotnet restore

# Build the project
echo -e "${YELLOW}Building the project...${NC}"
dotnet build

# Run the API
echo -e "${GREEN}Starting WebAPI...${NC}"
echo -e "${GREEN}======================================"
echo -e "${GREEN}API will be available at:${NC}"
echo -e "${GREEN}  HTTP:  http://localhost:5000${NC}"
echo -e "${GREEN}  HTTPS: https://localhost:5001${NC}"
echo -e "${GREEN}  Swagger: http://localhost:5000/swagger${NC}"
echo -e "${GREEN}======================================"
echo -e "${YELLOW}Press Ctrl+C to stop${NC}"

# Run with hot reload for development
dotnet watch run --urls "http://localhost:5000;https://localhost:5001"