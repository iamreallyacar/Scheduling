using Microsoft.EntityFrameworkCore;

namespace TireScheduler.Api.Database;

// Brings one module's schema up to date at startup (DEC-24).
internal sealed class MigrateDatabaseStep<TContext>(TContext context) : IStartupStep
    where TContext : DbContext
{
    public Task RunAsync(CancellationToken cancellationToken) => context.Database.MigrateAsync(cancellationToken);
}
