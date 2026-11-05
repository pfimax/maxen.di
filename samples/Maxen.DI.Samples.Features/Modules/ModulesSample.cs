namespace Maxen.DI.Samples.Features.Modules;

internal sealed class ModulesSample : ISample
{
    public string Name => "modules";
    public string Summary => "reusable registration bundles, and modules switched on by feature flags";

    public Task RunAsync()
    {
        // A module by type...
        var builder = new ContainerBuilder();
        builder.RegisterModule<ReportingModule>();
        using (var container = builder.Build())
            Console.WriteLine("module: " + container.Resolve<IReportPrinter>().Print());

        // (builder.RegisterModules(assembly) applies every module it finds in an assembly; here that would include
        // the beta module below, so this sample picks modules by type instead.)

        // A module behind a feature flag. Flip it with UseFeatures or the MAXEN_FEATURES variable.
        ContainerBuilder WithFeatures(params string[] features)
        {
            var flagged = new ContainerBuilder();
            flagged.RegisterModule<ReportingModule>();
            flagged.RegisterModule<BetaFeatureModule>("beta");
            flagged.UseFeatures(features);
            return flagged;
        }

        using var off = WithFeatures().Build();
        using var on = WithFeatures("beta").Build();
        Console.WriteLine("beta off: " + off.Resolve<IReportPrinter>().Print());
        Console.WriteLine("beta on:  " + on.Resolve<IReportPrinter>().Print());
        return Task.CompletedTask;
    }
}
