namespace Maxen.DI.Samples.Features.Modules;

public sealed class BetaPrinter : IReportPrinter
{
    public string Print() => "report printed with the new beta layout";
}
