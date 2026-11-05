using Maxen.DI.Extensions.DependencyInjection;
using Maxen.DI.Options;
using Maxen.DI.Tests.Fixtures;
using Microsoft.Extensions.Configuration;

namespace Maxen.DI.Tests;

public class ConfigurationBindingTests
{
    private static IConfiguration Configuration(params (string Key, string Value)[] values)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(v => v.Key, v => (string?)v.Value))
            .Build();

    [Fact]
    public void A_configuration_section_fills_the_options()
    {
        var configuration = Configuration(("Database:Host", "db.internal"), ("Database:Port", "6543"), ("Database:Retry:Count", "4"));

        using var container = TestContainer.Build(b =>
            b.BindOptions<DatabaseSettings>(configuration.GetSection("Database")));

        var settings = container.Resolve<IOptions<DatabaseSettings>>().Value;

        Assert.Equal("db.internal", settings.Host);
        Assert.Equal(6543, settings.Port);
        Assert.Equal(4, settings.Retry.Count);
    }

    [Fact]
    public void Missing_keys_keep_their_defaults()
    {
        var configuration = Configuration(("Database:Host", "db.internal"));

        using var container = TestContainer.Build(b =>
            b.BindOptions<DatabaseSettings>(configuration.GetSection("Database")));

        Assert.Equal(5432, container.Resolve<IOptions<DatabaseSettings>>().Value.Port);
    }

    [Fact]
    public void A_later_Configure_step_can_adjust_what_the_file_said()
    {
        var configuration = Configuration(("Database:Host", "from-file"));

        using var container = TestContainer.Build(b =>
        {
            b.BindOptions<DatabaseSettings>(configuration.GetSection("Database"));
            b.Configure<DatabaseSettings>(s => s.Host += "-tweaked");
        });

        Assert.Equal("from-file-tweaked", container.Resolve<IOptions<DatabaseSettings>>().Value.Host);
    }
}
