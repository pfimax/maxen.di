namespace Maxen.DI.Samples.Features.Lifetimes;

internal sealed class LifetimesSample : ISample
{
    public string Name => "lifetimes";
    public string Summary => "transient, scoped, singleton, per-thread, pooled and tag-scoped";

    public Task RunAsync()
    {
        var builder = new ContainerBuilder();
        // The same class under different keys, so the lifetimes can be compared side by side.
        builder.Register<Widget>().AsTransient().WithKey("transient");
        builder.Register<Widget>().AsScoped().WithKey("scoped");
        builder.Register<Widget>().AsSingleton().WithKey("singleton");
        builder.Register<Widget>().AsPerThread().WithKey("thread");
        builder.Register<Widget>().AsScopedTo("request").WithKey("request");
        builder.Register<PooledBuffer>().AsPooled(maxRetained: 4);

        using var container = builder.Build();
        using var one = container.CreateScope();
        using var two = container.CreateScope();

        Console.WriteLine($"transient: {one.Resolve<Widget>("transient").Id} then {one.Resolve<Widget>("transient").Id} (new every time)");
        Console.WriteLine($"scoped:    {one.Resolve<Widget>("scoped").Id} = {one.Resolve<Widget>("scoped").Id}, other scope gets {two.Resolve<Widget>("scoped").Id}");
        Console.WriteLine($"singleton: {one.Resolve<Widget>("singleton").Id} = {two.Resolve<Widget>("singleton").Id} (shared by everyone)");
        Console.WriteLine($"per-thread: {one.Resolve<Widget>("thread").Id} here, {OnAnotherThread(() => one.Resolve<Widget>("thread").Id)} on another thread");

        // A scope labelled "request" shares the instance with every scope nested inside it.
        using var request = container.CreateScope("request");
        using var nested = request.CreateScope();
        Console.WriteLine($"scoped to a tag: {request.Resolve<Widget>("request").Id} = {nested.Resolve<Widget>("request").Id}");

        // Pooled: when the scope ends the buffer goes back to the pool and the next scope reuses it.
        int first;
        using (var pooledScope = container.CreateScope())
            first = pooledScope.Resolve<PooledBuffer>().Id;
        using (var pooledScope = container.CreateScope())
        {
            var again = pooledScope.Resolve<PooledBuffer>();
            Console.WriteLine($"pooled: first scope used #{first}, second scope got #{again.Id} (Reset ran {again.Uses} time(s))");
        }

        return Task.CompletedTask;
    }

    // A real thread: Task.Run().Result can run the work on the calling thread, which would hide the difference.
    private static int OnAnotherThread(Func<int> work)
    {
        var result = 0;
        var thread = new Thread(() => result = work());
        thread.Start();
        thread.Join();
        return result;
    }
}
