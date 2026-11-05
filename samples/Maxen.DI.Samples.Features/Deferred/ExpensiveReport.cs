namespace Maxen.DI.Samples.Features.Deferred;

/// <summary>Pretends to be costly to build, so the output shows when it really is created.</summary>
internal sealed class ExpensiveReport
{
    public ExpensiveReport() => Console.WriteLine("  (ExpensiveReport built)");

    public string Title => "quarterly numbers";
}
