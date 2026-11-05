namespace Maxen.DI;

/// <summary>Implement this on pooled services to wipe their state before they go back into the pool.</summary>
public interface IPoolable
{
    /// <summary>Puts the object back to a clean state before it is lent out again.</summary>
    void Reset();
}
