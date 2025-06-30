# CORS Configuration Guide

## Overview

The Cross-Origin Resource Sharing (CORS) configuration has been enhanced to provide better security, validation, and flexibility across different environments.

## Key Improvements

### 1. URL Validation

- All CORS origins are now validated using proper URI parsing
- Invalid URLs are rejected during application startup
- No more fragile string replacement for protocol switching

### 2. Environment-Specific Security

- **Production**: Only HTTPS origins are allowed
- **Development**: Both HTTP and HTTPS are supported
- **Staging**: HTTPS enforced but multiple subdomains supported

### 3. Multiple Origins Support

- Configuration now supports arrays of allowed origins
- Each environment can have multiple valid frontends
- Automatic deduplication of origins

## Configuration Structure

### appsettings.json Format

```json
{
  "AppSettings": {
    "FrontendUrl": "http://localhost:5173",
    "AllowedOrigins": [
      "http://localhost:5173",
      "http://localhost:3000"
    ]
  }
}
```

### Environment-Specific Configurations

#### Development

- Supports both HTTP and HTTPS
- Includes localhost and 127.0.0.1 variants
- Automatically adds alternate protocol versions

#### Staging

- HTTPS only
- Multiple subdomain support
- Validates all URLs at startup

#### Production

- HTTPS only (enforced)
- Multiple production domains supported
- Strict validation

## Validation Rules

1. **URL Format**: Must be valid absolute URLs
2. **Protocol**: HTTP/HTTPS only
3. **Host**: Must have valid hostname
4. **Production**: HTTPS required
5. **Development**: Both protocols allowed

## Error Handling

### Startup Validation

- Invalid origins cause application startup failure
- Clear error messages indicate which URLs are invalid
- Production environments reject HTTP origins

### Runtime Behavior

- Only validated origins are added to CORS policy
- Logging shows exactly which origins are configured
- Zero origins result in warning but don't crash the app

## Migration from Old Configuration

### Old Format (Deprecated)

```json
{
  "AppSettings": {
    "FrontendUrl": "http://localhost:5173"
  }
}
```

### New Format (Recommended)

```json
{
  "AppSettings": {
    "FrontendUrl": "http://localhost:5173",
    "AllowedOrigins": [
      "http://localhost:5173",
      "http://localhost:3000"
    ]
  }
}
```

## Environment Variables for Production

For production deployments, you can override CORS configuration using environment variables:

```bash
# Single origin
AppSettings__FrontendUrl=https://yourapp.com

# Multiple origins (JSON array as string)
AppSettings__AllowedOrigins__0=https://yourapp.com
AppSettings__AllowedOrigins__1=https://www.yourapp.com
AppSettings__AllowedOrigins__2=https://app.yourapp.com
```

## Security Considerations

1. **Never use wildcards** in production CORS configuration
2. **Always use HTTPS** in staging and production
3. **Minimize origins** - only include necessary domains
4. **Regular audits** - review configured origins periodically
5. **Subdomain strategy** - plan your domain structure carefully

## Troubleshooting

### Common Issues

1. **CORS Error in Browser**
   - Check if your frontend URL is in the allowed origins
   - Verify the protocol (HTTP vs HTTPS) matches
   - Ensure credentials are properly configured

2. **Application Startup Failure**
   - Check application logs for CORS validation errors
   - Verify all origins are valid URLs
   - Ensure production origins use HTTPS

3. **Development Issues**
   - Both HTTP and HTTPS versions should be automatically included
   - Check if localhost vs 127.0.0.1 is causing issues
   - Verify port numbers match your dev server

### Debugging Commands

```bash
# Check configured origins
dotnet run --environment Development

# Validate production configuration
dotnet run --environment Production --no-launch-profile
```

## Best Practices

1. **Use environment-specific configurations** for different deployment stages
2. **Include both www and non-www** versions of production domains
3. **Test CORS configuration** in each environment before deployment
4. **Monitor CORS-related errors** in application logs
5. **Document all allowed origins** and their purposes
