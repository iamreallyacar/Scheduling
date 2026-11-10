using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace TireScheduler.Api.Tests;

public sealed class SchedulerApiFactory(string? connectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        if (connectionString is not null)
        {
            builder.UseSetting("ConnectionStrings:Scheduler", connectionString);
        }
    }
}
