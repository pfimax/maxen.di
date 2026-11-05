namespace Maxen.DI.Samples.Features;

/// <summary>One runnable example of a single feature.</summary>
internal interface ISample
{
    /// <summary>The word you type after <c>dotnet run --</c>.</summary>
    string Name { get; }

    /// <summary>One line on what it shows.</summary>
    string Summary { get; }

    Task RunAsync();
}
