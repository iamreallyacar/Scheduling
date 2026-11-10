using TireScheduler.Api.Database;
using TireScheduler.Api.Health;
using TireScheduler.Api.Modules.Auth;
using TireScheduler.Api.Modules.MasterData;
using TireScheduler.Api.Modules.Orders;
using TireScheduler.Api.Modules.Progress;
using TireScheduler.Api.Modules.Scheduling;
using TireScheduler.Api.Queries;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
// Server code reads "now" only through TimeProvider, so tests can fix the clock (ARCHITECTURE.md section 5).
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSchedulerDatabase();
builder.Services.AddSchedulerHealthChecks();

builder.Services.AddAuthModule();
builder.Services.AddMasterDataModule();
builder.Services.AddOrdersModule();
builder.Services.AddSchedulingModule();
builder.Services.AddProgressModule();
builder.Services.AddQueries();

WebApplication app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapSchedulerHealthEndpoints();

await app.RunAsync().ConfigureAwait(false);

// Makes the generated Program class visible to WebApplicationFactory in the API tests.
public partial class Program;
