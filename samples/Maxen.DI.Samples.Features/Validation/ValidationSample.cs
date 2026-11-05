using Maxen.DI.Diagnostics;

namespace Maxen.DI.Samples.Features.Validation;

internal sealed class ValidationSample : ISample
{
    public string Name => "validation";
    public string Summary => "find missing dependencies, loops and lifetime mistakes before they hurt";

    public Task RunAsync()
    {
        var builder = new ContainerBuilder();
        builder.Register<UserRepository>();          // needs Database, which nobody registered
        builder.Register<CurrentUser>().AsScoped();
        builder.Register<AuditLog>().AsSingleton();  // a singleton holding a scoped service

        using var container = builder.Build();

        // Nothing has been constructed: the graph is read from the constructors.
        foreach (var issue in container.Validate())
            Console.WriteLine("  " + issue);

        // ValidateOnBuild makes Build() itself refuse a broken setup, which is handy at startup.
        var strict = new ContainerBuilder();
        strict.Options.ValidateOnBuild = true;
        strict.Register<UserRepository>();
        try
        {
            strict.Build();
        }
        catch (ContainerValidationException ex)
        {
            Console.WriteLine($"Build() refused to start with {ex.Issues.Count} problem(s)");
        }

        return Task.CompletedTask;
    }
}
