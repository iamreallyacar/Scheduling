using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Login_and_Registration_Backend_.NET_.Services;
using Login_and_Registration_Backend_.NET_.Data;
using Login_and_Registration_Backend_.NET_.Models;
using Login_and_Registration_Backend_.NET_.Configuration;
using Login_and_Registration_Backend_.NET_.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Get environment and validate configuration
var environment = builder.Environment.EnvironmentName;
var logger = LoggerFactory.Create(config => config.AddConsole()).CreateLogger("Startup");

logger.LogInformation("Application starting in {Environment} environment", environment);

// Validate all required configuration using the helper
try
{
    ConfigurationHelper.ValidateConfiguration(builder.Configuration, environment);
    logger.LogInformation("Configuration validation passed");
}
catch (InvalidOperationException ex)
{
    logger.LogError("Configuration validation failed: {Error}", ex.Message);
    throw;
}

// Log configuration status (without revealing secrets)
logger.LogInformation("JWT Issuer: {JwtIssuer}", builder.Configuration["Jwt:Issuer"]);
logger.LogInformation("Frontend URL: {FrontendUrl}", builder.Configuration["AppSettings:FrontendUrl"]);

// Entity Framework and Database Configuration
if (builder.Environment.IsEnvironment("Testing"))
{
    // Use in-memory database for testing (will be overridden by test factory)
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseInMemoryDatabase("TestingDatabase")
    );
    logger.LogInformation("Using In-Memory database for testing");
}
else
{
    // Use environment-specific database configuration
    var connectionString = ConfigurationHelper.GetConnectionString(builder.Configuration, environment);
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlite(connectionString)
    );
    
    logger.LogInformation("Using SQLite database: {DatabaseFile}", 
        connectionString.Replace("Data Source=", "").Replace(";", ""));
}

// Identity services
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequiredLength = 8;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// SignInManager
builder.Services.AddScoped<SignInManager<ApplicationUser>>();

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Add CORS policy
builder.Services.AddCors(options =>
{
	// Use environment-specific allowed origins with validation
	options.AddPolicy("AllowFrontend", policy =>
	{
		try
		{
			var allowedOrigins = ConfigurationHelper.GetAllowedOrigins(builder.Configuration, environment);
			
			if (allowedOrigins.Length == 0)
			{
				logger.LogWarning("No valid CORS origins configured. API will reject all cross-origin requests.");
			}
			else
			{
				policy
					.WithOrigins(allowedOrigins)
					.AllowAnyHeader()
					.AllowAnyMethod()
					.AllowCredentials();
				
				logger.LogInformation("CORS configured for {Count} origins: {Origins}", 
					allowedOrigins.Length, string.Join(", ", allowedOrigins));
			}
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Failed to configure CORS origins");
			throw;
		}
	});
});

// Add Authentication with improved configuration
builder.Services.AddCustomAuthentication(builder.Configuration);

// Add Authorization with custom policies
builder.Services.AddCustomAuthorization();

builder.Services.AddControllers();

builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<Login_and_Registration_Backend_.NET_.Services.IAuthenticationService, Login_and_Registration_Backend_.NET_.Services.AuthenticationService>();
builder.Services.AddScoped<IDatabaseSeedingService, DatabaseSeedingService>();

var app = builder.Build();

// Initialize database and seed data (skip in testing environment)
if (!app.Environment.IsEnvironment("Testing"))
{
    await app.Services.InitializeDatabaseAsync();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
	app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Make the implicit Program class public so it can be referenced by tests
public partial class Program { }
