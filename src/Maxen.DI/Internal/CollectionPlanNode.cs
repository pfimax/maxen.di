namespace Maxen.DI.Internal;

/// <summary>
/// A collection parameter (<c>IEnumerable&lt;T&gt;</c>, <c>T[]</c>, ...): one array holding every registration of the
/// element type, in registration order. Like the framework's own collection source, the array is never cached and
/// never disposed; the items inside follow their own lifetimes.
/// </summary>
internal sealed class CollectionPlanNode : PlanNode
{
    private readonly Type _elementType;
    private readonly PlanNode[] _items;

    public CollectionPlanNode(Type elementType, PlanNode[] items)
    {
        _elementType = elementType;
        _items = items;
    }

    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Arrays of reference types and of types the application already uses are available ahead of time.")]
    public override object Get(IScope scope)
    {
        var array = Array.CreateInstance(_elementType, _items.Length);
        for (var i = 0; i < _items.Length; i++)
            array.SetValue(_items[i].Get(scope), i);
        return array;
    }
}
