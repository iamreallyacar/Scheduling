using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TireScheduler.Api.Modules.MasterData;
using TireScheduler.Api.Modules.MasterData.Calendars;
using TireScheduler.Api.Security;

namespace TireScheduler.Api.Tests.MasterData;

public sealed class MasterDataPersistenceTests(PostgresFixture postgres)
{
    [Fact]
    public async Task AC_40_new_database_has_one_plant_default_calendar_with_three_shifts_monday_to_friday()
    {
        await using SchedulerApiFactory factory = new(postgres.NewDatabaseConnectionString());
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        MasterDataDbContext context = scope.ServiceProvider.GetRequiredService<MasterDataDbContext>();

        Calendar calendar = Assert.Single(await context.Calendars.Include(c => c.Shifts).ToListAsync(TestContext.Current.CancellationToken));

        Assert.True(calendar.IsPlantDefault);
        DayOfWeek[] workingDays = [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];
        (int, int)[] threeShifts = [(0, 8 * 60), (8 * 60, 16 * 60), (16 * 60, 24 * 60)];
        Assert.Equal(
            workingDays.SelectMany(day => threeShifts.Select(shift => (day, shift.Item1, shift.Item2))),
            calendar.Shifts.OrderBy(s => s.Weekday).ThenBy(s => s.StartMinuteOfDay).Select(s => (s.Weekday, s.StartMinuteOfDay, s.EndMinuteOfDay)));
    }

    [Fact]
    public async Task AC_16_master_data_is_unchanged_after_a_restart()
    {
        string connectionString = postgres.NewDatabaseConnectionString();
        string machinesBeforeRestart;
        string productsBeforeRestart;
        await using (SchedulerApiFactory firstRun = new(connectionString))
        {
            using ApiSession admin = await firstRun.SignInAsRoleAsync(UserRole.Admin);
            int typeId = await MachineRegisterTests.CreateResourceTypeAsync(admin, "Curing press");
            await MachineRegisterTests.CreateResourceAsync(admin, "Press 1", typeId);
            await admin.PostAsync("/api/v1/products", new { code = "PS-205", name = "Passenger 205/55 R16", spec = "205/55 R16 91V" });
            machinesBeforeRestart = (await admin.GetJsonAsync("/api/v1/resources")).GetRawText();
            productsBeforeRestart = (await admin.GetJsonAsync("/api/v1/products")).GetRawText();
        }

        await using SchedulerApiFactory secondRun = new(connectionString);
        using ApiSession adminAfterRestart = await secondRun.SignInAsRoleAsync(UserRole.Admin);

        JsonElement machinesAfterRestart = await adminAfterRestart.GetJsonAsync("/api/v1/resources");
        Assert.Equal(1, machinesAfterRestart.GetArrayLength());
        Assert.Equal(machinesBeforeRestart, machinesAfterRestart.GetRawText());
        Assert.Equal(productsBeforeRestart, (await adminAfterRestart.GetJsonAsync("/api/v1/products")).GetRawText());
    }
}
