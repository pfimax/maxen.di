using Maxen.DI.Options;
using Maxen.DI.Tests.Fixtures;

namespace Maxen.DI.Tests;

public class OptionsTests
{
    [Fact]
    public void Configuration_steps_run_in_order_and_the_result_is_a_singleton()
    {
        using var container = TestContainer.Build(b =>
        {
            b.Configure<DatabaseSettings>(s => s.Host = "first");
            b.Configure<DatabaseSettings>(s => s.Host += "-second");
        });

        var options = container.Resolve<IOptions<DatabaseSettings>>();

        Assert.Equal("first-second", options.Value.Host);
        Assert.Same(options, container.Resolve<IOptions<DatabaseSettings>>());
    }

    [Fact]
    public void Values_are_bound_from_flat_pairs_including_nested_sections()
    {
        var values = new Dictionary<string, string?>
        {
            ["database:host"] = "db.internal",
            ["Database:Port"] = "6543",
            ["Database:UseSsl"] = "true",
            ["Database:Timeout"] = "00:00:05",
            ["Database:Mode"] = "readonly",
            ["Database:Retry:Count"] = "4",
        };
        using var container = TestContainer.Build(b => b.BindOptions<DatabaseSettings>(values, "Database"));

        var settings = container.Resolve<IOptions<DatabaseSettings>>().Value;

        Assert.Equal("db.internal", settings.Host);
        Assert.Equal(6543, settings.Port);
        Assert.True(settings.UseSsl);
        Assert.Equal(TimeSpan.FromSeconds(5), settings.Timeout);
        Assert.Equal(DatabaseMode.ReadOnly, settings.Mode);
        Assert.Equal(4, settings.Retry.Count);
    }

    [Fact]
    public void Missing_keys_keep_their_defaults()
    {
        using var container = TestContainer.Build(b =>
            b.BindOptions<DatabaseSettings>(new Dictionary<string, string?> { ["Port"] = "1" }));

        var settings = container.Resolve<IOptions<DatabaseSettings>>().Value;

        Assert.Equal(1, settings.Port);
        Assert.Equal("localhost", settings.Host);
    }

    [Fact]
    public void Bad_values_produce_a_readable_error()
    {
        var builder = new ContainerBuilder();
        builder.BindOptions<DatabaseSettings>(new Dictionary<string, string?> { ["Port"] = "not-a-number" });
        using var container = builder.Build();

        var error = Assert.ThrowsAny<Exception>(() => container.Resolve<IOptions<DatabaseSettings>>());

        Assert.Contains("Port", error.ToString());
    }
}
