using System.Text;
using Maxen.DI.Tests.Fixtures;

namespace Maxen.DI.Tests;

/// <summary>
/// Same idea as <see cref="PlanComparisonTests"/> for collection parameters: a consumer that takes every greeter, with
/// all lifetime combinations of the consumer, the three greeters and the clock one of them needs.
/// </summary>
public class PlanCollectionComparisonTests
{
    private static readonly Lifetime[] Lifetimes = { Lifetime.Transient, Lifetime.Scoped, Lifetime.Singleton };

    // 3^5 = 243 combinations; a normal run takes every third, CI takes all (see PlanComparisonTests).
    public static IEnumerable<object[]> Combinations()
    {
        var step = Environment.GetEnvironmentVariable("MAXEN_EXHAUSTIVE") == "1" ? 1 : 3;
        for (var code = 0; code < 243; code += step) yield return new object[] { code };
    }

    private static Lifetime[] Decode(int code)
    {
        var result = new Lifetime[5];
        for (var i = 0; i < result.Length; i++, code /= 3) result[i] = Lifetimes[code % 3];
        return result;
    }

    private static string Run(int code, bool plans)
    {
        var lifetimes = Decode(code);
        var builder = new ContainerBuilder();
        builder.Options.UseResolvePlans = plans;
        builder.Register<GreeterCollection>().WithLifetime(lifetimes[0]);
        builder.Register<IGreeter, QuietGreeter>().WithLifetime(lifetimes[1]);
        builder.Register<IGreeter, LoudGreeterWithoutDependencies>().WithLifetime(lifetimes[2]);
        builder.Register<IGreeter, Greeter>().WithLifetime(lifetimes[3]);
        builder.Register<IClock, Clock>().WithLifetime(lifetimes[4]);

        using var container = builder.Build();
        using var one = container.CreateScope();
        using var two = container.CreateScope();

        var ids = new Dictionary<object, int>(ReferenceEqualityComparer.Instance);
        int Id(object o)
        {
            if (!ids.TryGetValue(o, out var id)) ids[o] = id = ids.Count;
            return id;
        }

        var log = new StringBuilder();
        foreach (var (name, scope) in new (string, IScope)[] { ("one", one), ("one", one), ("two", two), ("root", container) })
        {
            try
            {
                var consumer = scope.Resolve<GreeterCollection>();
                var items = consumer.All.Select(g => g is Greeter greeter
                    ? $"{g.GetType().Name}#{Id(g)}(clock#{Id(greeter.Clock)})"
                    : $"{g.GetType().Name}#{Id(g)}");
                log.Append($"[{name}] consumer#{Id(consumer)}: {string.Join(", ", items)}\n");
            }
            catch (Exception ex)
            {
                log.Append($"[{name}] {ex.GetType().Name}: {ex.Message}\n");
            }
        }
        return log.ToString();
    }

    [Theory]
    [MemberData(nameof(Combinations))]
    public void Collections_resolve_identically_with_and_without_plans(int code)
    {
        var withPlans = Run(code, plans: true);
        var without = Run(code, plans: false);

        Assert.True(withPlans == without,
            $"Lifetimes {string.Join(",", Decode(code).Select(l => l.Name))} differ.\n--- plans ---\n{withPlans}\n--- pipeline ---\n{without}");
    }

    [Fact]
    public void A_collection_parameter_is_covered_by_the_plan()
    {
        var builder = new ContainerBuilder();
        builder.Register<GreeterCollection>();
        builder.Register<IGreeter, QuietGreeter>();
        builder.Register<IGreeter, LoudGreeterWithoutDependencies>();
        using var container = builder.Build();

        Assert.Equal(2, container.Resolve<GreeterCollection>().All.Count);
        Assert.True(container.FindDescriptor(new ServiceKey(typeof(GreeterCollection)))!.Plan!.IsUsable);
    }
}
