namespace Maxen.DI.Samples.Features.Generics;

internal sealed class GenericsSample : ISample
{
    public string Name => "generics";
    public string Summary => "one open generic registration serves every closed type";

    public Task RunAsync()
    {
        var builder = new ContainerBuilder();
        builder.Register(typeof(IRepository<>), typeof(Repository<>)).AsSingleton();

        using var container = builder.Build();
        Console.WriteLine(container.Resolve<IRepository<string>>().Describe());
        Console.WriteLine(container.Resolve<IRepository<int>>().Describe());

        // A closed registration for one type wins over the open one.
        var special = new ContainerBuilder();
        special.Register(typeof(IRepository<>), typeof(Repository<>));
        special.Register<IRepository<Guid>, GuidRepository>();
        using var other = special.Build();
        Console.WriteLine(other.Resolve<IRepository<Guid>>().Describe());
        return Task.CompletedTask;
    }
}
