using Login_and_Registration_Backend_.NET_.Services;

namespace Login_and_Registration_Backend_.NET_.Extensions
{
    /// <summary>
    /// Extension methods for IServiceProvider to handle database operations
    /// </summary>
    public static class ServiceProviderExtensions
    {
        /// <summary>
        /// Initializes the database by applying migrations and seeding initial data
        /// </summary>
        /// <param name="serviceProvider">The service provider</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the async operation</returns>
        public static async Task InitializeDatabaseAsync(
            this IServiceProvider serviceProvider, 
            CancellationToken cancellationToken = default)
        {
            using var scope = serviceProvider.CreateScope();
            var seedingService = scope.ServiceProvider.GetRequiredService<IDatabaseSeedingService>();
            await seedingService.SeedDatabaseAsync(cancellationToken);
        }

        /// <summary>
        /// Applies pending migrations to the database without seeding
        /// </summary>
        /// <param name="serviceProvider">The service provider</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the async operation</returns>
        public static async Task MigrateDatabaseAsync(
            this IServiceProvider serviceProvider, 
            CancellationToken cancellationToken = default)
        {
            using var scope = serviceProvider.CreateScope();
            var seedingService = scope.ServiceProvider.GetRequiredService<IDatabaseSeedingService>();
            await seedingService.MigrateDatabaseAsync(cancellationToken);
        }
    }
}
