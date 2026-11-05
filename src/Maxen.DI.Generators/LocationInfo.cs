using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Maxen.DI.Generators
{
    /// <summary>
    /// A source location that can be compared by value. Roslyn's own Location holds on to syntax trees,
    /// which would defeat the generator's incremental caching, so we keep only the plain numbers.
    /// </summary>
    internal sealed record LocationInfo(string FilePath, TextSpan Span, LinePositionSpan LineSpan)
    {
        public static LocationInfo From(Location location)
            => new LocationInfo(location.SourceTree?.FilePath ?? string.Empty, location.SourceSpan, location.GetLineSpan().Span);

        public Location ToLocation() => Location.Create(FilePath, Span, LineSpan);
    }
}
