namespace Maxen.DI.Arguments;

/// <summary>One value in a <see cref="ResolveArguments"/>: matched by name, or by type when there is no name.</summary>
internal sealed record ResolveArgumentEntry(string? Name, Type? Type, object? Value);
