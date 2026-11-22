using TireScheduler.PlantLoader;

namespace TireScheduler.PlantGenerator.Tests;

public sealed class LoaderCommandLineTests
{
    private static readonly DateOnly Wednesday = new(2026, 11, 18);

    private static readonly Dictionary<string, string> SignInSettings = new()
    {
        [LoaderCommandLine.EmailVariable] = "admin@plant.test",
        [LoaderCommandLine.PasswordVariable] = "First-Admin-1",
    };

    [Fact]
    public void Every_option_is_read()
    {
        LoaderSettings settings = Parse(["--preset", "small", "--seed", "-12", "--start", "2026-12-07", "--api", "https://scheduler.test"]);

        Assert.Equal("small", settings.PresetName);
        Assert.Same(PlantPresets.Small, settings.Parameters);
        Assert.Equal((-12, new DateOnly(2026, 12, 7)), (settings.Seed, settings.StartDate));
        Assert.Equal(new Uri("https://scheduler.test"), settings.ApiUrl);
        Assert.Equal(("admin@plant.test", "First-Admin-1"), (settings.Email, settings.Password));
    }

    [Theory]
    [InlineData("2026-11-18", "2026-11-16")]
    [InlineData("2026-11-16", "2026-11-16")]
    [InlineData("2026-11-22", "2026-11-16")]
    public void A32_without_options_the_seed_is_1_the_api_local_and_the_start_the_monday_of_the_current_week(string todayUtc, string expectedStart)
    {
        LoaderSettings settings = LoaderCommandLine.Parse(["--preset", "plant"], SignInSettings.GetValueOrDefault, DateOnly.Parse(todayUtc, System.Globalization.CultureInfo.InvariantCulture));

        Assert.Equal((1, DateOnly.Parse(expectedStart, System.Globalization.CultureInfo.InvariantCulture)), (settings.Seed, settings.StartDate));
        Assert.Equal(new Uri("http://localhost:5080"), settings.ApiUrl);
    }

    [Theory]
    [InlineData("--preset", new[] { "--seed", "1" })]
    [InlineData("--preset", new[] { "--preset", "huge" })]
    [InlineData("--seed", new[] { "--preset", "small", "--seed", "abc" })]
    [InlineData("--seed", new[] { "--preset", "small", "--seed" })]
    [InlineData("--start", new[] { "--preset", "small", "--start", "2026-13-01" })]
    [InlineData("--api", new[] { "--preset", "small", "--api", "not a url" })]
    [InlineData("--fast", new[] { "--preset", "small", "--fast" })]
    public void AC_160_an_unreadable_command_line_fails_naming_the_option(string expectedOption, string[] arguments)
    {
        LoaderUsageException failure = Assert.Throws<LoaderUsageException>(() => Parse(arguments));

        Assert.Contains(expectedOption, failure.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(LoaderCommandLine.EmailVariable)]
    [InlineData(LoaderCommandLine.PasswordVariable)]
    public void AC_160_missing_sign_in_settings_fail_naming_the_setting(string missingVariable)
    {
        Dictionary<string, string> environment = new(SignInSettings);
        environment.Remove(missingVariable);

        LoaderUsageException failure = Assert.Throws<LoaderUsageException>(() =>
            LoaderCommandLine.Parse(["--preset", "small"], environment.GetValueOrDefault, Wednesday));

        Assert.Contains(missingVariable, failure.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1, new[] { "--preset", "small", "--schedule" })]
    [InlineData(4, new[] { "--schedule", "--preset", "small", "--seed", "4" })]
    public void AC_203_the_schedule_option_takes_no_value_and_asks_for_a_placed_schedule(int expectedSeed, string[] arguments)
    {
        LoaderSettings settings = Parse(arguments);

        Assert.True(settings.PlaceSchedule);
        Assert.Equal(expectedSeed, settings.Seed);
    }

    [Fact]
    public void AC_203_without_the_schedule_option_no_schedule_is_placed()
    {
        Assert.False(Parse(["--preset", "small"]).PlaceSchedule);
    }

    [Fact]
    public void AC_160_a_value_after_the_schedule_option_fails_naming_it()
    {
        LoaderUsageException failure = Assert.Throws<LoaderUsageException>(() => Parse(["--preset", "small", "--schedule", "yes"]));

        Assert.Contains("yes", failure.Message, StringComparison.Ordinal);
    }

    private static LoaderSettings Parse(string[] arguments) => LoaderCommandLine.Parse(arguments, SignInSettings.GetValueOrDefault, Wednesday);
}
