namespace Maxen.DI.Options;

/// <summary>Gives access to a configured settings object. Inject <c>IOptions&lt;MySettings&gt;</c> where you need it.</summary>
public interface IOptions<out T> where T : class
{
    /// <summary>The settings, with every registered configuration step already applied.</summary>
    T Value { get; }
}
