using Maxen.DI.Internal;
using Maxen.DI.Tests.Fixtures;

namespace Maxen.DI.Tests;

/// <summary>
/// A constructor is called through reflection at first and gets a compiled call once it has been used a few times.
/// Which of the two is in use must not show: same objects, same failures.
/// </summary>
public class ConstructorPlanTests
{
    private static ConstructorPlan PlanFor<T>() => new(typeof(T).GetConstructors().Single());

    // Uses the constructor until its compiled call has arrived from the background thread.
    private static Func<object?[], object> Compiled(ConstructorPlan plan, object?[] good)
    {
        var first = plan.FromArray;
        Assert.True(
            SpinWait.SpinUntil(() =>
            {
                plan.FromArray(good);
                return !ReferenceEquals(plan.FromArray, first);
            }, TimeSpan.FromSeconds(30)),
            "The compiled call never arrived.");
        return plan.FromArray;
    }

    [Fact]
    public void A_constructor_used_once_is_not_compiled()
    {
        var plan = PlanFor<TieredThing>();

        var thing = (TieredThing)plan.FromArray(new object?[] { 7, "seven" });

        Assert.Equal(7, thing.Number);
        Assert.Equal("seven", thing.Name);

        // Compiling is only ordered on the second use, so there is no compiled call to hand out yet.
        Assert.Null(plan.Fixed);
    }

    [Fact]
    public void A_constructor_used_again_and_again_gets_a_compiled_call_that_builds_the_same_object()
    {
        var plan = PlanFor<TieredThing>();
        var arguments = new object?[] { 7, "seven" };

        var compiled = Compiled(plan, arguments);
        var thing = (TieredThing)compiled(arguments);

        Assert.Equal(7, thing.Number);
        Assert.Equal("seven", thing.Name);
    }

    [Fact]
    public void The_direct_call_with_typed_arguments_arrives_too()
    {
        var plan = PlanFor<TieredThing>();

        Assert.True(SpinWait.SpinUntil(() => plan.Fixed is not null, TimeSpan.FromSeconds(30)));

        var build = (Func<object?, object?, object>)plan.Fixed!;
        Assert.Equal("seven", ((TieredThing)build(7, "seven")).Name);
    }

    [Fact]
    public void An_argument_of_the_wrong_type_fails_the_same_way_before_and_after_compiling()
    {
        var plan = PlanFor<TieredThing>();
        var wrong = new object?[] { 7, 8 };

        var before = Assert.Throws<InvalidCastException>(() => plan.FromArray(wrong));
        var compiled = Compiled(plan, new object?[] { 7, "seven" });
        var after = Assert.Throws<InvalidCastException>(() => compiled(wrong));

        Assert.Equal(after.Message, before.Message);
    }

    [Fact]
    public void A_null_for_a_value_type_fails_the_same_way_before_and_after_compiling()
    {
        var plan = PlanFor<TieredThing>();
        var missing = new object?[] { null, "seven" };

        Assert.Throws<NullReferenceException>(() => plan.FromArray(missing));
        var compiled = Compiled(plan, new object?[] { 7, "seven" });
        Assert.Throws<NullReferenceException>(() => compiled(missing));
    }

    [Fact]
    public void A_null_for_a_reference_type_is_passed_through_either_way()
    {
        var plan = PlanFor<TieredThing>();
        var arguments = new object?[] { 7, null };

        Assert.Null(((TieredThing)plan.FromArray(arguments)).Name);
        Assert.Null(((TieredThing)Compiled(plan, arguments)(arguments)).Name);
    }

    [Fact]
    public void An_exception_from_the_constructor_arrives_as_it_was_thrown()
    {
        using var container = TestContainer.Build(b =>
        {
            b.RegisterInstance("clock");
            b.Register<ThrowingThing>();
        });

        // Often enough to pass from the reflection call to the compiled one; the caller must not see the difference.
        for (var i = 0; i < 50; i++)
        {
            var error = Assert.Throws<InvalidOperationException>(() => container.Resolve<ThrowingThing>());
            Assert.Equal("no clock today", error.Message);
        }
    }

    [Fact]
    public void A_service_resolved_many_times_is_the_same_before_and_after_compiling()
    {
        using var container = TestContainer.Build(b =>
        {
            b.Register<IClock, Clock>().AsSingleton();
            b.Register<IGreeter, Greeter>();
        });

        var clock = container.Resolve<IClock>();
        for (var i = 0; i < 200; i++)
        {
            var greeter = container.Resolve<IGreeter>();
            Assert.IsType<Greeter>(greeter);
            if (i % 20 == 0) Thread.Sleep(1);
        }

        Assert.Same(clock, container.Resolve<IClock>());
    }
}
