using Maxen.DI.Arguments;
using Maxen.DI.Tests.Fixtures;

namespace Maxen.DI.Tests;

public class ResolveArgumentsTests
{
    private static Container BuildWithReport(Action<RegistrationBuilder>? tweak = null)
        => TestContainer.Build(b =>
        {
            b.Register<IClock, Clock>();
            var registration = b.Register<Report>().WithRuntimeArguments();
            tweak?.Invoke(registration);
        });

    [Fact]
    public void Arguments_are_matched_by_parameter_name()
    {
        using var container = BuildWithReport();

        var report = container.ResolveWith<Report>(new { title = "Q3", pages = 12 });

        Assert.Equal("Q3", report.Title);
        Assert.Equal(12, report.Pages);
        Assert.NotNull(report.Clock);
    }

    [Fact]
    public void Arguments_can_be_matched_by_type()
    {
        using var container = BuildWithReport();

        var report = container.ResolveWith<Report>(new ResolveArguments().Add("Annual"));

        Assert.Equal("Annual", report.Title);
    }

    [Fact]
    public void Func_with_arguments_acts_as_an_auto_factory()
    {
        using var container = BuildWithReport();

        var factory = container.Resolve<Func<string, Report>>();

        Assert.Equal("A", factory("A").Title);
        Assert.Equal("B", factory("B").Title);
    }

    [Fact]
    public void Func_with_two_arguments_hands_them_out_by_type_and_position()
    {
        using var container = BuildWithReport();

        var factory = container.Resolve<Func<string, int, Report>>();
        var report = factory("Monthly", 3);

        Assert.Equal("Monthly", report.Title);
        Assert.Equal(3, report.Pages);
    }

    [Fact]
    public void Only_transient_services_accept_arguments()
    {
        using var container = BuildWithReport(r => r.AsSingleton());

        Assert.Throws<ResolutionException>(() => container.ResolveWith<Report>(new { title = "x" }));
    }

    [Fact]
    public void Validation_trusts_registrations_that_take_runtime_arguments()
    {
        using var container = BuildWithReport();

        Assert.Empty(container.Validate());
    }

    [Fact]
    public void Without_the_arguments_the_service_cannot_be_built()
    {
        using var container = BuildWithReport();

        Assert.Throws<ResolutionException>(() => container.Resolve<Report>());
    }
}
