using Microsoft.CodeAnalysis;

namespace Maxen.DI.Tests;

/// <summary>
/// The references the generator and analyzer tests compile their snippets against: everything this test run has loaded.
/// </summary>
internal static class CompilationReferences
{
    /// <summary>
    /// Every loaded assembly that is still there to read. Assemblies in a collectible context are left out: the tool
    /// tests load throwaway assemblies that way and then delete them, and on Linux the delete really happens, so a
    /// later compile that picked them up would fail with a missing file. Nothing a snippet needs lives in one of those.
    /// </summary>
    public static IEnumerable<MetadataReference> All()
        => AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !a.IsCollectible && !string.IsNullOrEmpty(a.Location) && File.Exists(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location));
}
