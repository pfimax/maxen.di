using Maxen.DI.Tests.Fixtures;
using Microsoft.Extensions.Logging;

namespace Maxen.DI.Tests;

public class LoggingTests
{
    [Fact]
    public void Successful_resolutions_are_logged_at_the_chosen_level()
    {
        var logger = new CapturingLogger();
        using var container = TestContainer.Build(b =>
        {
            b.Register<IClock, Clock>();
            b.LogResolutions(logger, LogLevel.Information);
        });

        container.Resolve<IClock>();

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Contains("IClock", entry.Message);
    }

    [Fact]
    public void Failures_are_logged_as_errors_with_the_exception()
    {
        var logger = new CapturingLogger();
        using var container = TestContainer.Build(b =>
        {
            b.RegisterFactory<Thrower>(_ => throw new InvalidOperationException("boom"));
            b.LogResolutions(logger);
        });

        Assert.ThrowsAny<Exception>(() => container.Resolve<Thrower>());

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.NotNull(entry.Error);
    }

    [Fact]
    public void Slow_resolutions_are_warnings()
    {
        var logger = new CapturingLogger();
        using var container = TestContainer.Build(b =>
        {
            b.RegisterFactory<IClock>(_ => { Thread.Sleep(20); return new Clock(); });
            b.LogResolutions(logger, slowThreshold: TimeSpan.FromMilliseconds(5));
        });

        container.Resolve<IClock>();

        Assert.Equal(LogLevel.Warning, Assert.Single(logger.Entries).Level);
    }
}
