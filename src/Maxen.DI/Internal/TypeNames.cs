namespace Maxen.DI.Internal;

/// <summary>Formats types the way a person would write them, for error messages.</summary>
internal static class TypeNames
{
    /// <summary>Turns <c>List`1</c> into <c>List&lt;String&gt;</c>.</summary>
    public static string Format(Type type)
    {
        if (!type.IsGenericType)
            return type.Name;

        var name = type.Name;
        var tick = name.IndexOf('`');
        if (tick >= 0) name = name[..tick];

        var args = string.Join(", ", type.GetGenericArguments().Select(Format));
        return $"{name}<{args}>";
    }
}
