using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Login_and_Registration_Backend_.NET_.Services;
using Login_and_Registration_Backend_.NET_.Data;
using Login_and_Registration_Backend_.NET_.Models;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls("http://localhost:5000");

// Entity Framework and Database Configuration
if (builder.Environment.IsEnvironment("Testing"))
{
    // Use in-memory database for testing (will be overridden by test factory)
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseInMemoryDatabase("TestingDatabase")
    );
}
else
{
    // Use SQLite for development and production
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection"))
    );
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
	// Allow both HTTP and HTTPS for local frontend
	options.AddPolicy("AllowFrontend", policy =>
	{
		var frontendUrl = builder.Configuration["AppSettings:FrontendUrl"] ?? "http://localhost:5173";
		policy
			.WithOrigins(
				frontendUrl,
				frontendUrl.Replace("http://", "https://")
			)
			.AllowAnyHeader()
			.AllowAnyMethod()
			.AllowCredentials();
	});
});

// Add Authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
{
    options.LoginPath = "/api/auth/google-login";
    options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
    options.SlidingExpiration = true;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key is not configured"))
        )
    };
})
.AddGoogle(options =>
{
	options.ClientId = builder.Configuration["Authentication:Google:ClientId"] ?? throw new InvalidOperationException("Google ClientId is not configured");
	options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"] ?? throw new InvalidOperationException("Google ClientSecret is not configured");
	options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
});

builder.Services.AddControllers();

builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IUserService, UserService>();

var app = builder.Build();

// Ensure database is created (skip in testing environment)
if (!app.Environment.IsEnvironment("Testing"))
{
    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.Database.EnsureCreated();
        
        // Seed tire production machines if they don't exist
        SeedTireProductionData(context);
    }
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

// Data seeding method for tire production
static void SeedTireProductionData(ApplicationDbContext context)
{
    // Seed Machines if they don't exist
    if (!context.Machines.Any())
    {        var tireMachines = new[]
        {
            new Machine
            {
                Name = "Tire Molding Press 1",
                Type = "Molding Press",
                Status = "idle",
                Utilization = 0,
                IsActive = true,
                LastMaintenance = DateTime.Now.AddDays(-30),
                NextMaintenance = DateTime.Now.AddDays(30),
                Notes = "Primary tire molding press for passenger car tires"
            },
            new Machine
            {
                Name = "Tire Molding Press 2",
                Type = "Molding Press", 
                Status = "idle",
                Utilization = 0,
                IsActive = true,
                LastMaintenance = DateTime.Now.AddDays(-25),
                NextMaintenance = DateTime.Now.AddDays(35),
                Notes = "Secondary tire molding press for high-performance tires"
            },
            new Machine
            {
                Name = "Tire Building Machine 1",
                Type = "Building Machine",
                Status = "idle",
                Utilization = 0,
                IsActive = true,
                LastMaintenance = DateTime.Now.AddDays(-20),
                NextMaintenance = DateTime.Now.AddDays(40),
                Notes = "Automated tire building for consistent quality"
            },
            new Machine
            {
                Name = "Tread Extrusion Line 1",
                Type = "Extrusion Line",
                Status = "idle",
                Utilization = 0,
                IsActive = true,
                LastMaintenance = DateTime.Now.AddDays(-15),
                NextMaintenance = DateTime.Now.AddDays(45),
                Notes = "High-capacity tread extrusion for various tire sizes"
            },
            new Machine
            {
                Name = "Quality Control Station",
                Type = "QC Station",
                Status = "idle",
                Utilization = 0,
                IsActive = true,
                LastMaintenance = DateTime.Now.AddDays(-10),
                NextMaintenance = DateTime.Now.AddDays(50),
                Notes = "Final quality inspection and testing station"
            }
        };

        context.Machines.AddRange(tireMachines);
        context.SaveChanges();
        
        Console.WriteLine("Seeded tire production machines successfully!");
    }
}

// Make the implicit Program class public so it can be referenced by tests
public partial class Program { }
