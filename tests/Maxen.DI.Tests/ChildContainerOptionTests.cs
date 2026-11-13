using Maxen.DI.Diagnostics;
using Maxen.DI.Tests.Fixtures;

namespace Maxen.DI.Tests;

/// <summary>A child container starts with every switch set the way its parent has it, and may change any of them.</summary>
public class ChildContainerOptionTests
{
    [Fact]
    public void Every_option_is_taken_over_from_the_parent()
    {
        using var parent = TestContainer.Build(b =>
        {
            b.Options.ValidateScopes = false;
            b.Options.ValidateOnBuild = true;
            b.Options.ValidateFactories = true;
            b.Options.UseResolvePlans = false;
            b.Options.DisposeChildScopes = true;
        });

        using var child = parent.CreateChild(_ => { });

        Assert.False(child.Options.ValidateScopes);
        Assert.True(child.Options.ValidateOnBuild);
        Assert.True(child.Options.ValidateFactories);
        Assert.False(child.Options.UseResolvePlans);
        Assert.True(child.Options.DisposeChildScopes);
    }

    [Fact]
    public void The_child_can_still_choose_differently_without_changing_the_parent()
    {
        using var parent = TestContainer.Build(b => b.Options.DisposeChildScopes = true);

        using var child = parent.CreateChild(c => c.Options.DisposeChildScopes = false);

        Assert.False(child.Options.DisposeChildScopes);
        Assert.True(parent.Options.DisposeChildScopes);
    }

    [Fact]
    public void A_child_of_a_parent_that_checks_factories_checks_its_own()
    {
        using var parent = TestContainer.Build(b => b.Options.ValidateFactories = true);
        using var child = parent.CreateChild(c =>
            c.RegisterFactory<IClock>(_ => throw new InvalidOperationException("no clock today")));

        var issue = Assert.Single(child.Validate());
        Assert.Equal(ValidationIssueKind.FactoryFailed, issue.Kind);
    }

    [Fact]
    public void A_child_of_a_parent_that_disposes_child_scopes_does_so_too()
    {
        using var parent = TestContainer.Build(b =>
        {
            b.Options.DisposeChildScopes = true;
            b.Register<Disposable>().AsScoped();
        });
        var child = parent.CreateChild(_ => { });
        var scope = child.CreateScope();
        var scoped = scope.Resolve<Disposable>();

        child.Dispose();

        Assert.True(scoped.Disposed);
    }
}
