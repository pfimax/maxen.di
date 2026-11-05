using Microsoft.CodeAnalysis;

namespace Maxen.DI.Generators
{
    /// <summary>A problem found while reading a class, kept as plain data until it is reported.</summary>
    internal sealed record DiagnosticModel(DiagnosticDescriptor Descriptor, LocationInfo Location, string Argument);
}
