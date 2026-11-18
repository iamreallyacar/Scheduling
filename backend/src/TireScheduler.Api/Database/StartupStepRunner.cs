namespace TireScheduler.Api.Database;

public static class StartupStepRunner
{
    public static async Task RunStartupStepsAsync(this WebApplication app)
    {
        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        foreach (IStartupStep step in scope.ServiceProvider.GetServices<IStartupStep>())
        {
            await step.RunAsync(app.Lifetime.ApplicationStopping);
        }
    }
}
