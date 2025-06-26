# Secret Management Guide

## Overview

This application uses a layered configuration approach:
- **Base Configuration**: `appsettings.json` (committed, no secrets)
- **Environment-Specific**: `appsettings.{Environment}.json` (committed, placeholders only)
- **Secrets**: User Secrets (development) / Environment Variables (production/staging)

## Development Environment

### Using User Secrets (Recommended for Development)

1. **Initialize user secrets** (already done):
   ```bash
   cd Login-and-Registration-Backend-.NET-
   dotnet user-secrets init
   ```

2. **Set required secrets**:
   ```bash
   # JWT Secret Key (generate a new one for your project)
   dotnet user-secrets set "Jwt:Key" "your-super-secure-jwt-key-at-least-32-characters-long"
   
   # Google OAuth Credentials (get from Google Cloud Console)
   dotnet user-secrets set "Authentication:Google:ClientId" "your-google-client-id"
   dotnet user-secrets set "Authentication:Google:ClientSecret" "your-google-client-secret"
   ```

3. **View current secrets**:
   ```bash
   dotnet user-secrets list
   ```

4. **Remove a secret**:
   ```bash
   dotnet user-secrets remove "Jwt:Key"
   ```

## Staging Environment

### Using Environment Variables

Set these environment variables in your staging environment:

```bash
# Environment
ASPNETCORE_ENVIRONMENT=Staging

# JWT Configuration
JWT__KEY=your-staging-jwt-key
JWT__ISSUER=SchedulingAPI-Staging
JWT__AUDIENCE=SchedulingUsers-Staging

# Google OAuth (staging credentials)
AUTHENTICATION__GOOGLE__CLIENTID=your-staging-google-client-id
AUTHENTICATION__GOOGLE__CLIENTSECRET=your-staging-google-client-secret

# Database
CONNECTIONSTRINGS__DEFAULTCONNECTION=your-staging-connection-string

# Frontend URL
APPSETTINGS__FRONTENDURL=https://staging.your-domain.com
```

## Production Environment

### Using Environment Variables

Set these environment variables in your production environment:

```bash
# Environment
ASPNETCORE_ENVIRONMENT=Production

# JWT Configuration
JWT__KEY=your-production-jwt-key
JWT__ISSUER=SchedulingAPI
JWT__AUDIENCE=SchedulingUsers

# Google OAuth (production credentials)
AUTHENTICATION__GOOGLE__CLIENTID=your-production-google-client-id
AUTHENTICATION__GOOGLE__CLIENTSECRET=your-production-google-client-secret

# Database
CONNECTIONSTRINGS__DEFAULTCONNECTION=your-production-connection-string

# Frontend URL
APPSETTINGS__FRONTENDURL=https://your-production-domain.com
```

# Frontend URL
APPSETTINGS__FRONTENDURL=https://your-production-domain.com
```

### Using Azure Key Vault (Recommended for Production)

1. **Install Azure Key Vault package**:
   ```bash
   dotnet add package Azure.Extensions.AspNetCore.Configuration.Secrets
   ```

2. **Update Program.cs** to use Key Vault:
   ```csharp
   if (builder.Environment.IsProduction())
   {
       var keyVaultUrl = builder.Configuration["KeyVaultUrl"];
       if (!string.IsNullOrEmpty(keyVaultUrl))
       {
           builder.Configuration.AddAzureKeyVault(new Uri(keyVaultUrl), new DefaultAzureCredential());
       }
   }
   ```

## Security Best Practices

1. **Never commit secrets to source control**
2. **Use different secrets for different environments**
3. **Rotate secrets regularly**
4. **Use managed identity in Azure for Key Vault access**
5. **Monitor secret access and usage**

## Generating Secure JWT Keys

Use a cryptographically secure random generator:

```bash
# PowerShell
[System.Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))

# Or online tool (for development only):
# https://generate-random.org/api-key-generator?count=1&length=64&type=mixed-numbers
```
