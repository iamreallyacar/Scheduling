using Microsoft.Extensions.Configuration;

namespace Login_and_Registration_Backend_.NET_.Configuration;

public static class ConfigurationHelper
{
    /// <summary>
    /// Validates that all required configuration values are present
    /// </summary>
    public static void ValidateConfiguration(IConfiguration configuration, string environment)
    {
        var requiredKeys = new[]
        {
            "Jwt:Key",
            "Jwt:Issuer", 
            "Jwt:Audience",
            "Authentication:Google:ClientId",
            "Authentication:Google:ClientSecret",
            "AppSettings:FrontendUrl"
        };

        var missingKeys = new List<string>();

        foreach (var key in requiredKeys)
        {
            var value = configuration[key];
            if (string.IsNullOrWhiteSpace(value))
            {
                missingKeys.Add(key);
            }
        }

        if (missingKeys.Any())
        {
            var errorMessage = environment switch
            {
                "Development" => $"Missing configuration keys: {string.Join(", ", missingKeys)}. " +
                               "Please set them using dotnet user-secrets. See SECRETS_MANAGEMENT.md for details.",
                "Production" or "Staging" => $"Missing configuration keys: {string.Join(", ", missingKeys)}. " +
                                           "Please set the corresponding environment variables. See SECRETS_MANAGEMENT.md for details.",
                _ => $"Missing configuration keys: {string.Join(", ", missingKeys)}. Check your configuration setup."
            };
            
            throw new InvalidOperationException(errorMessage);
        }

        // Validate CORS origins
        ValidateCorsConfiguration(configuration, environment);
    }

    /// <summary>
    /// Validates CORS configuration to ensure origins are properly formatted
    /// </summary>
    private static void ValidateCorsConfiguration(IConfiguration configuration, string environment)
    {
        var origins = GetConfiguredOrigins(configuration, environment);
        var invalidOrigins = new List<string>();
        var httpOriginsInProduction = new List<string>();

        foreach (var origin in origins)
        {
            if (!IsValidOrigin(origin, ""))
            {
                invalidOrigins.Add(origin);
            }
            else if (environment == "Production" && Uri.TryCreate(origin, UriKind.Absolute, out var uri) && 
                     uri.Scheme == Uri.UriSchemeHttp)
            {
                httpOriginsInProduction.Add(origin);
            }
        }

        if (invalidOrigins.Any())
        {
            throw new InvalidOperationException(
                $"Invalid CORS origins found: {string.Join(", ", invalidOrigins)}. " +
                "Origins must be valid HTTP or HTTPS URLs.");
        }

        // In production, ensure all origins use HTTPS
        if (httpOriginsInProduction.Any())
        {
            throw new InvalidOperationException(
                $"HTTP origins not allowed in production: {string.Join(", ", httpOriginsInProduction)}. " +
                "All origins must use HTTPS in production.");
        }
    }

    /// <summary>
    /// Gets environment-specific connection string
    /// </summary>
    public static string GetConnectionString(IConfiguration configuration, string environment)
    {
        return environment switch
        {
            "Testing" => "InMemory", // Will be handled separately
            "Development" => configuration.GetConnectionString("DefaultConnection") ?? "Data Source=auth-dev.db;",
            "Staging" => configuration.GetConnectionString("DefaultConnection") ?? "Data Source=staging.db;",
            "Production" => configuration.GetConnectionString("DefaultConnection") ?? "Data Source=production.db;",
            _ => configuration.GetConnectionString("DefaultConnection") ?? "Data Source=auth.db;"
        };
    }

    /// <summary>
    /// Gets environment-specific CORS origins with proper URL validation
    /// </summary>
    public static string[] GetAllowedOrigins(IConfiguration configuration, string environment)
    {
        var configuredOrigins = GetConfiguredOrigins(configuration, environment);
        var validatedOrigins = new List<string>();

        foreach (var origin in configuredOrigins)
        {
            if (IsValidOrigin(origin, environment))
            {
                validatedOrigins.Add(origin);
                
                // For development, also add the alternate protocol version
                if (environment == "Development")
                {
                    var alternateOrigin = GetAlternateProtocolOrigin(origin);
                    if (alternateOrigin != null && IsValidOrigin(alternateOrigin, environment))
                    {
                        validatedOrigins.Add(alternateOrigin);
                    }
                }
            }
        }

        // If no valid origins found, fall back to environment defaults
        if (validatedOrigins.Count == 0)
        {
            var defaultOrigins = GetEnvironmentDefaultOrigins(environment);
            validatedOrigins.AddRange(defaultOrigins);
        }

        return validatedOrigins.Distinct().ToArray();
    }

    /// <summary>
    /// Gets default origins for each environment
    /// </summary>
    private static string[] GetEnvironmentDefaultOrigins(string environment)
    {
        return environment switch
        {
            "Development" => new[] { "http://localhost:5173", "https://localhost:5173" },
            "Staging" => new[] { "https://staging.your-domain.com" },
            "Production" => new[] { "https://your-production-domain.com" },
            _ => new[] { "http://localhost:5173" }
        };
    }

    /// <summary>
    /// Gets configured origins from configuration or fallback defaults
    /// </summary>
    private static string[] GetConfiguredOrigins(IConfiguration configuration, string environment)
    {
        // Try to get from configuration section first (supports multiple origins)
        var configSection = configuration.GetSection("AppSettings:AllowedOrigins");
        if (configSection.Exists())
        {
            var origins = configSection.Get<string[]>();
            if (origins != null && origins.Length > 0)
            {
                return origins;
            }
        }

        // Fallback to single FrontendUrl
        var frontendUrl = configuration["AppSettings:FrontendUrl"];
        if (!string.IsNullOrEmpty(frontendUrl))
        {
            return new[] { frontendUrl };
        }

        // Environment-specific defaults
        return environment switch
        {
            "Development" => new[] { "http://localhost:5173" },
            "Staging" => new[] { "https://staging.your-domain.com" },
            "Production" => new[] { "https://your-production-domain.com" },
            _ => new[] { "http://localhost:5173" }
        };
    }

    /// <summary>
    /// Validates if a URL is a valid origin for CORS
    /// </summary>
    private static bool IsValidOrigin(string origin, string environment = "")
    {
        if (string.IsNullOrWhiteSpace(origin))
            return false;

        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
            return false;

        // Must be HTTP or HTTPS
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return false;

        // Must have a valid host
        if (string.IsNullOrWhiteSpace(uri.Host))
            return false;

        // For production, enforce HTTPS
        if (environment == "Production" && uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Gets the alternate protocol version of an origin (HTTP <-> HTTPS)
    /// </summary>
    private static string? GetAlternateProtocolOrigin(string origin)
    {
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
            return null;

        var alternateScheme = uri.Scheme == Uri.UriSchemeHttp 
            ? Uri.UriSchemeHttps 
            : Uri.UriSchemeHttp;

        var alternateUri = new UriBuilder(uri)
        {
            Scheme = alternateScheme,
            Port = alternateScheme == Uri.UriSchemeHttps ? 443 : 80
        };

        // For localhost, preserve the original port for development
        if (uri.Host == "localhost" && uri.Port != -1)
        {
            alternateUri.Port = uri.Port;
        }

        return alternateUri.ToString().TrimEnd('/');
    }

    /// <summary>
    /// Determines if detailed error information should be shown
    /// </summary>
    public static bool ShouldShowDetailedErrors(string environment)
    {
        return environment == "Development" || environment == "Staging";
    }

    /// <summary>
    /// Gets environment-specific log level configuration
    /// </summary>
    public static LogLevel GetDefaultLogLevel(string environment)
    {
        return environment switch
        {
            "Development" => LogLevel.Information,
            "Staging" => LogLevel.Information,
            "Production" => LogLevel.Warning,
            _ => LogLevel.Information
        };
    }
}
