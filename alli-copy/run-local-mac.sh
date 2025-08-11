#!/bin/bash

echo "Setting up ASP.NET WebAPI on Mac..."

# Check if Mono is installed
if ! command -v mono &> /dev/null; then
    echo "Mono is not installed. Installing via Homebrew..."
    brew install mono
fi

# Check if NuGet is installed
if ! command -v nuget &> /dev/null; then
    echo "Installing NuGet..."
    brew install nuget
fi

# Restore NuGet packages
echo "Restoring NuGet packages..."
nuget restore Alliance.Wholesale.Gravitate.sln

# Build the WebAPI project
echo "Building Custom.Gravitate.WebAPI..."
msbuild Custom.Gravitate.WebAPI/Custom.Gravitate.WebAPI.csproj /p:Configuration=Debug /p:Platform="Any CPU"

# Run with XSP4 (Mono's development web server)
echo "Starting WebAPI on http://localhost:8080..."
cd Custom.Gravitate.WebAPI
xsp4 --port 8080 --nonstop