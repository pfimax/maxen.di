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

        // Two things validation would otherwise have to take on trust.
        var wrapped = new ContainerBuilder();

        // A decorator is built together with the service it wraps, so what it needs is checked as that service's own.
        // CachedUserDirectory takes a Database, which nobody registered.
        wrapped.Register<IUserDirectory, UserDirectory>();
        wrapped.Decorate<IUserDirectory, CachedUserDirectory>();

        // A factory is a closed box, unless it says what it resolves. This one resolves a CurrentUser that isn't there.
        wrapped.RegisterFactory<AuditLog>(ctx => new AuditLog(ctx.Resolve<CurrentUser>()))
            .WithMetadata(ServiceMetadata.Dependencies, new[] { ServiceKey.For<CurrentUser>() });

        using (var checkedContainer = wrapped.Build())
        {
            foreach (var issue in checkedContainer.Validate())
                Console.WriteLine("  " + issue);
        }

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
