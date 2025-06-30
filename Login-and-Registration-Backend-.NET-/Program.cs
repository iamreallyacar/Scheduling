using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Login_and_Registration_Backend_.NET_.Services;
using Login_and_Registration_Backend_.NET_.Data;
using Login_and_Registration_Backend_.NET_.Models;
using Login_and_Registration_Backend_.NET_.Configuration;
using Login_and_Registration_Backend_.NET_.Extensions;
using Login_and_Registration_Backend_.NET_.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// Simple logging setup
var logger = LoggerFactory.Create(config => config.AddConsole()).CreateLogger("Startup");
logger.LogInformation("Application starting in {Environment} environment", builder.Environment.EnvironmentName);

// Quick configuration validation
try
{
    ConfigurationHelper.ValidateConfiguration(builder.Configuration, builder.Environment.EnvironmentName);
    logger.LogInformation("Configuration validation passed");
}
catch (Exception ex)
{
    logger.LogError("Configuration error: {Error}", ex.Message);
    throw;
}

// Database setup - simple and clean
if (builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseInMemoryDatabase("TestingDatabase"));
}
else
{
    var connectionString = ConfigurationHelper.GetConnectionString(builder.Configuration, builder.Environment.EnvironmentName);
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlite(connectionString));
}

// Identity setup
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

// All services in one place
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<Login_and_Registration_Backend_.NET_.Services.IAuthenticationService, Login_and_Registration_Backend_.NET_.Services.AuthenticationService>();
builder.Services.AddScoped<IDatabaseSeedingService, DatabaseSeedingService>();
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Simple CORS setup
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        var frontendUrl = builder.Configuration["AppSettings:FrontendUrl"] ?? "http://localhost:3000";
        policy.WithOrigins(frontendUrl)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Simple authentication setup
builder.Services.AddCustomAuthentication(builder.Configuration);
builder.Services.AddCustomAuthorization();

// Health checks
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database")
    .AddCheck<JwtServiceHealthCheck>("jwt-service");

var app = builder.Build();

// Simple database initialization
if (!app.Environment.IsEnvironment("Testing"))
{
    await app.Services.InitializeDatabaseAsync();
}

// Clean middleware pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

// Make the implicit Program class public so it can be referenced by tests
public partial class Program { }
