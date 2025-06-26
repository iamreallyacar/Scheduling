#!/usr/bin/env pwsh

# Environment Configuration Test Script
# Run this script to test the configuration setup in different environments

Write-Host "=== Environment Configuration Test ===" -ForegroundColor Cyan
Write-Host ""

# Function to test configuration in an environment
function Test-Environment {
    param(
        [string]$Environment,
        [string]$Description
    )
    
    Write-Host "Testing $Description ($Environment)..." -ForegroundColor Yellow
    
    # Set environment variable
    $env:ASPNETCORE_ENVIRONMENT = $Environment
    
    # Test configuration validation
    try {
        $output = dotnet run --project "Login-and-Registration-Backend-.NET-" --no-build --verbosity quiet 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ Configuration valid" -ForegroundColor Green
        } else {
            Write-Host "❌ Configuration validation failed:" -ForegroundColor Red
            Write-Host $output -ForegroundColor Red
        }
    } catch {
        Write-Host "❌ Error: $($_.Exception.Message)" -ForegroundColor Red
    }
    
    Write-Host ""
}

# Change to the backend directory
Set-Location "Login-and-Registration-Backend-.NET-"

# Test different environments
Write-Host "Note: This test expects user secrets to be configured for Development environment" -ForegroundColor Cyan
Write-Host "and environment variables for Production/Staging environments." -ForegroundColor Cyan
Write-Host ""

Test-Environment "Development" "Development Environment"
Test-Environment "Staging" "Staging Environment" 
Test-Environment "Production" "Production Environment"

# Reset environment
$env:ASPNETCORE_ENVIRONMENT = "Development"

Write-Host "=== Test Complete ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "If any tests failed, check:" -ForegroundColor Yellow
Write-Host "1. User secrets are configured for Development (see SECRETS_MANAGEMENT.md)" -ForegroundColor White
Write-Host "2. Environment variables are set for Production/Staging" -ForegroundColor White
Write-Host "3. Configuration files have the correct structure" -ForegroundColor White
