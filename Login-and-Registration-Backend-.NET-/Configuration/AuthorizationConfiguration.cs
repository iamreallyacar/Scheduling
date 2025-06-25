using Microsoft.AspNetCore.Authorization;

namespace Login_and_Registration_Backend_.NET_.Configuration
{
    public static class AuthorizationConfiguration
    {
        public static IServiceCollection AddCustomAuthorization(this IServiceCollection services)
        {
            services.AddAuthorization(options =>
            {
                // Default policy requires JWT authentication
                options.DefaultPolicy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .AddAuthenticationSchemes("Bearer")
                    .Build();

                // API policy specifically for JWT bearer tokens
                options.AddPolicy("ApiPolicy", policy =>
                {
                    policy.RequireAuthenticatedUser();
                    policy.AddAuthenticationSchemes("Bearer");
                });

                // OAuth policy for OAuth-specific endpoints
                options.AddPolicy("OAuthPolicy", policy =>
                {
                    policy.RequireAuthenticatedUser();
                    policy.AddAuthenticationSchemes("OAuth");
                });

                // Admin policy (example for future use)
                options.AddPolicy("AdminPolicy", policy =>
                {
                    policy.RequireAuthenticatedUser();
                    policy.AddAuthenticationSchemes("Bearer");
                    policy.RequireClaim("role", "admin");
                });

                // User policy (example for future use)
                options.AddPolicy("UserPolicy", policy =>
                {
                    policy.RequireAuthenticatedUser();
                    policy.AddAuthenticationSchemes("Bearer");
                    policy.RequireClaim("role", "user", "admin");
                });
            });

            return services;
        }
    }
}
