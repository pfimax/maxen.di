namespace Maxen.DI.Options;

/// <summary>Lets a configuration step be written as a lambda.</summary>
internal sealed class DelegateConfigureOptions<T> : IConfigureOptions<T> where T : class
{
    private readonly Action<T> _action;

    public DelegateConfigureOptions(Action<T> action) => _action = action;

    public void Configure(T options) => _action(options);
}
