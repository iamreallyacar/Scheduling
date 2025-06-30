using Login_and_Registration_Backend_.NET_.Data;

namespace Login_and_Registration_Backend_.NET_.Services
{
    /// <summary>
    /// Interface for database seeding operations
    /// </summary>
    public interface IDatabaseSeedingService
    {
        /// <summary>
        /// Seeds the database with initial data
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the async operation</returns>
        Task SeedDatabaseAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Seeds the database with environment-specific data
        /// </summary>
        /// <param name="environment">The environment name (Development, Staging, Production)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the async operation</returns>
        Task SeedForEnvironmentAsync(string environment, CancellationToken cancellationToken = default);

        /// <summary>
        /// Seeds tire production machines if they don't exist
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the async operation</returns>
        Task SeedTireProductionMachinesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Seeds production orders if they don't exist
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the async operation</returns>
        Task SeedProductionOrdersAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Cleans all seed data from the database
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the async operation</returns>
        Task CleanSeedDataAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Applies pending migrations to the database
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task representing the async operation</returns>
        Task MigrateDatabaseAsync(CancellationToken cancellationToken = default);
    }
}
