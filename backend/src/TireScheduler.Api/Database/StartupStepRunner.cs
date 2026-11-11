namespace TireScheduler.Api.Database;

public static class StartupStepRunner
{
    public static async Task RunStartupStepsAsync(this WebApplication app)
    {
        AsyncServiceScope scope = app.Services.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            foreach (IStartupStep step in scope.ServiceProvider.GetServices<IStartupStep>())
            {
                await step.RunAsync(app.Lifetime.ApplicationStopping).ConfigureAwait(false);
            }
        }
    }
}
