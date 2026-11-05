using Maxen.DI.Diagnostics;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Maxen.DI.Tests.Fixtures;

namespace Maxen.DI.Tests;

public class TelemetryTests
{
    // Listeners are process-wide and tests run in parallel, so every test only looks at its own service types.
    private static ActivityListener Listen(Action<Activity> onStopped)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == MaxenTelemetry.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = onStopped,
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    private static string? Service(Activity activity) => activity.GetTagItem("maxen.service")?.ToString();

    [Fact]
    public void Each_resolution_becomes_a_span_nested_like_the_dependency_chain()
    {
        var spans = new List<Activity>();
        using var listener = Listen(a => { lock (spans) spans.Add(a); });

        using var container = TestContainer.Build(b =>
        {
            b.Register<IClock, Clock>().AsSingleton();
            b.Register<IGreeter, Greeter>();
            b.UseTelemetry();
        });
        container.Resolve<IGreeter>();

        Activity greeter, clock;
        lock (spans)
        {
            greeter = Assert.Single(spans, s => Service(s) == "IGreeter");
            clock = Assert.Single(spans, s => Service(s) == "IClock");
        }

        Assert.Equal(greeter.Id, clock.ParentId);
        Assert.Equal("Singleton", clock.GetTagItem("maxen.lifetime"));
    }

    [Fact]
    public void A_failed_resolution_marks_its_span_as_an_error()
    {
        var spans = new List<Activity>();
        using var listener = Listen(a => { lock (spans) spans.Add(a); });

        using var container = TestContainer.Build(b =>
        {
            b.RegisterFactory<Thrower>(_ => throw new InvalidOperationException("boom"));
            b.UseTelemetry();
        });
        Assert.ThrowsAny<Exception>(() => container.Resolve<Thrower>());

        Activity failed;
        lock (spans) failed = spans.Last(s => Service(s) == "Thrower");
        Assert.Equal(ActivityStatusCode.Error, failed.Status);
    }

    [Fact]
    public void Resolutions_are_counted_and_timed()
    {
        long count = 0;
        var timed = 0;
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == MaxenTelemetry.Name) l.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
        {
            foreach (var tag in tags)
                if (tag.Key == "maxen.service" && tag.Value?.ToString() == "Greeter") Interlocked.Add(ref count, value);
        });
        listener.SetMeasurementEventCallback<double>((_, _, tags, _) =>
        {
            foreach (var tag in tags)
                if (tag.Key == "maxen.service" && tag.Value?.ToString() == "Greeter") Interlocked.Increment(ref timed);
        });
        listener.Start();

        using var container = TestContainer.Build(b =>
        {
            b.Register<IClock, Clock>();
            b.Register<Greeter>();
            b.UseTelemetry();
        });
        container.Resolve<Greeter>();
        container.Resolve<Greeter>();

        Assert.Equal(2, Interlocked.Read(ref count));
        Assert.Equal(2, Volatile.Read(ref timed));
    }

    [Fact]
    public void Tracing_can_be_turned_off_on_its_own()
    {
        var spans = 0;
        using var listener = Listen(a =>
        {
            if (Service(a) == "NeedsClock") Interlocked.Increment(ref spans);
        });

        using var container = TestContainer.Build(b =>
        {
            b.Register<IClock, Clock>();
            b.Register<NeedsClock>();
            b.UseTelemetry(tracing: false);
        });
        container.Resolve<NeedsClock>();

        Assert.Equal(0, Volatile.Read(ref spans));
    }
}
