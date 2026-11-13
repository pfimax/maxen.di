using Maxen.DI.Diagnostics;
using Maxen.DI.Tests.Fixtures;

namespace Maxen.DI.Tests;

/// <summary>
/// Validation looks for lifetime mismatches along every path, and a service many others depend on is reached by many
/// paths. The check must not redo the work below such a service for each path, and skipping it must not lose a
/// mismatch that only shows on one of them.
/// </summary>
public class ValidationSharingTests
{
    [Fact]
    public async Task A_deep_graph_of_shared_services_validates()
    {
        // Every level takes the level below twice, so forty levels is a million million paths.
        using var container = TestContainer.Build(b =>
        {
            b.Register<PlanPairLeaf>().AsSingleton();

            var type = typeof(PlanPairLeaf);
            for (var i = 0; i < 40; i++)
            {
                type = typeof(PlanPair<>).MakeGenericType(type);
                b.Register(type, type).AsSingleton();
            }
        });

        // On a background thread with a limit, so walking every path fails the test instead of hanging the run.
        var validation = Task.Run(() => container.Validate());
        var first = await Task.WhenAny(validation, Task.Delay(TimeSpan.FromSeconds(30)));

        Assert.True(ReferenceEquals(first, validation), "Validate() did not finish: it is walking every path.");
        Assert.Empty(await validation);
    }

    [Fact]
    public void A_mismatch_below_a_shared_service_is_reported_for_every_holder()
    {
        // Two singletons reach the scoped leaf through the same transient. The second one finds the transient already
        // walked, and still has to be told.
        using var container = TestContainer.Build(b =>
        {
            b.Register<PlanPairLeaf>().AsScoped();
            b.Register<PlanSharedMiddle>();
            b.Register<PlanSharedHolder>().AsSingleton();
            b.Register<PlanSharedTop>().AsSingleton();
        });

        var messages = container.Validate()
            .Where(i => i.Kind == ValidationIssueKind.LifetimeMismatch)
            .Select(i => i.Message)
            .ToList();

        Assert.Equal(2, messages.Count);
        Assert.Contains(messages, m => m.EndsWith("Path: PlanSharedHolder -> PlanSharedMiddle -> PlanPairLeaf"));
        Assert.Contains(messages, m => m.EndsWith("Path: PlanSharedTop -> PlanSharedMiddle -> PlanPairLeaf"));
    }

    [Fact]
    public void Each_mismatch_is_reported_once_however_many_paths_show_it()
    {
        // The singleton pair reaches the scoped leaf through both of its parameters.
        using var container = TestContainer.Build(b =>
        {
            b.Register<PlanPairLeaf>().AsScoped();
            b.Register<PlanPair<PlanPairLeaf>>();
            b.Register<PlanPair<PlanPair<PlanPairLeaf>>>().AsSingleton();
        });

        var issue = Assert.Single(container.Validate(), i => i.Kind == ValidationIssueKind.LifetimeMismatch);
        Assert.EndsWith("Path: PlanPair<PlanPair<PlanPairLeaf>> -> PlanPair<PlanPairLeaf> -> PlanPairLeaf", issue.Message);
    }
}
