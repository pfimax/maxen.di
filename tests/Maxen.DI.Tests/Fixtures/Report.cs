namespace Maxen.DI.Tests.Fixtures;

/// <summary>Needs a clock from the container and a title that only the caller knows.</summary>
public class Report
{
    public Report(IClock clock, string title, int pages = 1)
    {
        Clock = clock;
        Title = title;
        Pages = pages;
    }

    public IClock Clock { get; }
    public string Title { get; }
    public int Pages { get; }
}
