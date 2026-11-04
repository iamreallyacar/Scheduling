using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace Login_and_Registration_Backend_.NET_.Configuration
{
    public static class AuthenticationConfiguration
    {
        public static IServiceCollection AddCustomAuthentication(this IServiceCollection services, IConfiguration configuration)
        {
            // Define authentication schemes with clear separation
            services.AddAuthentication(options =>
            {
                // JWT as primary for API endpoints
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                ConfigureJwtBearer(options, configuration);
            })
            .AddCookie("OAuth", options =>
            {
                ConfigureOAuthCookie(options);
            })
            .AddGoogle(GoogleDefaults.AuthenticationScheme, options =>
            {
                ConfigureGoogleOAuth(options, configuration);
            });

            return services;
        }

        private static void ConfigureJwtBearer(JwtBearerOptions options, IConfiguration configuration)
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = configuration["Jwt:Issuer"],
                ValidAudience = configuration["Jwt:Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(configuration["Jwt:Key"]!)
                ),
                ClockSkew = TimeSpan.FromMinutes(5) // Allow 5 minutes clock skew
            };

            // Configure JWT events for better error handling
            options.Events = new JwtBearerEvents
            {
                OnAuthenticationFailed = context =>
                {
                    var logger = context.HttpContext.RequestServices.GetService<ILogger<Program>>();
                    logger?.LogWarning("JWT Authentication failed: {Error}", context.Exception.Message);
                    return Task.CompletedTask;
                },
                OnTokenValidated = context =>
                {
                    var logger = context.HttpContext.RequestServices.GetService<ILogger<Program>>();
                    logger?.LogDebug("JWT token validated for user: {UserId}", 
                        context.Principal?.FindFirst("sub")?.Value);
                    return Task.CompletedTask;
                }
            };
        }

        private static void ConfigureOAuthCookie(CookieAuthenticationOptions options)
        {
            options.LoginPath = "/api/auth/google-login";
            options.LogoutPath = "/api/auth/logout";
            options.ExpireTimeSpan = TimeSpan.FromMinutes(15); // Short-lived for OAuth flow
            options.SlidingExpiration = false; // Fixed expiration for OAuth
            options.Cookie.Name = "OAuth.Session";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Lax;
            
            // Configure events for OAuth cookie
            options.Events = new CookieAuthenticationEvents
            {
                OnRedirectToLogin = context =>
                {
                    // For API calls, return 401 instead of redirecting
                    if (context.Request.Path.StartsWithSegments("/api"))
                    {
                        context.Response.StatusCode = 401;
                        return Task.CompletedTask;
                    }
                    context.Response.Redirect(context.RedirectUri);
                    return Task.CompletedTask;
                }
            };
        }

        private static void ConfigureGoogleOAuth(GoogleOptions options, IConfiguration configuration)
        {
            options.ClientId = configuration["Authentication:Google:ClientId"]!;
            options.ClientSecret = configuration["Authentication:Google:ClientSecret"]!;
            options.SignInScheme = "OAuth"; // Use our dedicated OAuth cookie scheme
            
            // Request additional scopes if needed
            options.Scope.Add("email");
            options.Scope.Add("profile");
            
            // Save tokens for potential future use
            options.SaveTokens = true;
            
            options.Events = new Microsoft.AspNetCore.Authentication.OAuth.OAuthEvents
            {
                OnCreatingTicket = context =>
                {
                    var logger = context.HttpContext.RequestServices.GetService<ILogger<Program>>();
                    logger?.LogDebug("Google OAuth ticket created for user: {Email}", 
                        context.Principal?.FindFirst("email")?.Value);
                    return Task.CompletedTask;
                }
            };
        }
    }
}
