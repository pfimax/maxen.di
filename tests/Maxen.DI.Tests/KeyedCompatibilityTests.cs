using Maxen.DI.Diagnostics;
using Maxen.DI.Extensions.DependencyInjection;
using Maxen.DI.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace Maxen.DI.Tests;

/// <summary>
/// Microsoft's keyed-service conventions ([FromKeyedServices], [ServiceKey], KeyedService.AnyKey) used straight on a
/// ContainerBuilder, without the adapter. Microsoft's own conformance suite covers the adapter side.
/// </summary>
public class KeyedCompatibilityTests
{
    [Fact]
    public void FromKeyedServices_picks_the_keyed_registration()
    {
        var fast = new Clock();
        using var container = TestContainer.Build(b =>
        {
            b.Register<IClock, Clock>();
            b.RegisterInstance<IClock>(fast).WithKey("fast");
            b.Register<KeyedClockUser>();
        });

        Assert.Same(fast, container.Resolve<KeyedClockUser>().Clock);
    }

    [Fact]
    public void FromKeyedServices_without_a_key_uses_the_key_of_the_service_being_built()
    {
        var red = new Clock();
        var blue = new Clock();
        using var container = TestContainer.Build(b =>
        {
            b.RegisterInstance<IClock>(red).WithKey("red");
            b.RegisterInstance<IClock>(blue).WithKey("blue");
            b.Register<KeyInheritingClockUser>().WithKey("red");
            b.Register<KeyInheritingClockUser>().WithKey("blue");
        });

        Assert.Same(red, container.Resolve<KeyInheritingClockUser>("red").Clock);
        Assert.Same(blue, container.Resolve<KeyInheritingClockUser>("blue").Clock);
    }

    [Fact]
    public void ServiceKey_receives_the_key_and_unkeyed_registrations_fall_back_to_another_constructor()
    {
        using var container = TestContainer.Build(b =>
        {
            b.Register<KeyAware>().WithKey("alpha");
            b.Register<KeyAware>().WithKey("beta");
            b.Register<KeyAware>();
        });

        // Same class three times: the constructor choice must not be shared between the registrations.
        Assert.Equal("alpha", container.Resolve<KeyAware>("alpha").Key);
        Assert.Equal("beta", container.Resolve<KeyAware>("beta").Key);
        Assert.Equal("none", container.Resolve<KeyAware>().Key);
    }

    [Fact]
    public void A_key_of_the_wrong_type_for_ServiceKey_is_an_error_not_a_bad_cast()
    {
        using var container = TestContainer.Build(b =>
        {
            b.Register<IntKeyAware>().WithKey(7);
            b.Register<IntKeyAware>().WithKey("seven");
        });

        Assert.Equal(7, container.Resolve<IntKeyAware>(7).Key);
        Assert.Throws<ResolutionException>(() => container.Resolve<IntKeyAware>("seven"));
    }

    [Fact]
    public void An_any_key_registration_answers_unregistered_keys_with_one_instance_per_key()
    {
        using var container = TestContainer.Build(b => b.Register<KeyAware>().WithKey(ServiceKey.AnyKey).AsSingleton());

        var first = container.Resolve<KeyAware>("first");

        Assert.Equal("first", first.Key);
        Assert.Same(first, container.Resolve<KeyAware>("first"));
        Assert.Equal("second", container.Resolve<KeyAware>("second").Key);
        Assert.False(container.IsRegistered<KeyAware>());
    }

    [Fact]
    public void A_registration_with_the_exact_key_beats_the_any_key_one_whatever_the_order()
    {
        using var container = TestContainer.Build(b =>
        {
            b.Register<KeyAware>().WithKey("exact");
            b.RegisterFactory(_ => new KeyAware("from any-key")).WithKey(ServiceKey.AnyKey);
        });

        Assert.Equal("exact", container.Resolve<KeyAware>("exact").Key);
        Assert.Equal("from any-key", container.Resolve<KeyAware>("other").Key);
    }

    [Fact]
    public void Any_key_as_a_collection_lists_every_keyed_registration_but_never_resolves_alone()
    {
        var a = new Clock();
        var b1 = new Clock();
        var b2 = new Clock();
        using var container = TestContainer.Build(b =>
        {
            b.RegisterInstance<IClock>(new Clock());
            b.RegisterInstance<IClock>(a).WithKey("a");
            b.RegisterInstance<IClock>(b1).WithKey("b");
            b.RegisterFactory<IClock>(_ => new Clock()).WithKey(ServiceKey.AnyKey);
            b.RegisterInstance<IClock>(b2).WithKey("b");
        });

        // Registration order, unkeyed and catch-all registrations left out.
        Assert.Equal(new IClock[] { a, b1, b2 }, container.Resolve<IEnumerable<IClock>>(ServiceKey.AnyKey));
        Assert.Throws<ResolutionException>(() => container.Resolve<IClock>(ServiceKey.AnyKey));
        Assert.Throws<ResolutionException>(() => container.TryResolve(new ServiceKey(typeof(IClock), ServiceKey.AnyKey)));
    }

    [Fact]
    public void A_read_only_collection_of_singletons_is_built_once_but_arrays_are_fresh_copies()
    {
        using var container = TestContainer.Build(b =>
        {
            b.Register<IClock, Clock>().AsSingleton();
            b.Register<IClock, FrozenClock>().AsSingleton();
        });

        Assert.Same(container.Resolve<IEnumerable<IClock>>(), container.Resolve<IEnumerable<IClock>>());
        Assert.NotSame(container.Resolve<IClock[]>(), container.Resolve<IClock[]>());
    }

    [Fact]
    public void A_collection_with_a_transient_member_is_rebuilt_each_time()
    {
        using var container = TestContainer.Build(b =>
        {
            b.Register<IClock, Clock>().AsSingleton();
            b.Register<IClock, FrozenClock>();
        });

        Assert.NotSame(container.Resolve<IEnumerable<IClock>>(), container.Resolve<IEnumerable<IClock>>());
    }

    [Fact]
    public void Validation_understands_the_Microsoft_attributes()
    {
        using var valid = TestContainer.Build(b =>
        {
            b.Register<IClock, Clock>().WithKey("red");
            b.Register<KeyInheritingClockUser>().WithKey("red");
            b.Register<KeyAware>().WithKey(ServiceKey.AnyKey);
            b.Register<IntKeyAware>().WithKey(3);
        });
        Assert.Empty(valid.Validate());

        // "fast" was never registered, and the unkeyed clock must not stand in for it.
        using var invalid = TestContainer.Build(b =>
        {
            b.Register<IClock, Clock>();
            b.Register<KeyedClockUser>();
        });
        var issue = Assert.Single(invalid.Validate());
        Assert.Equal(ValidationIssueKind.MissingDependency, issue.Kind);
    }

    [Fact]
    public void The_dependency_graph_follows_inherited_keys()
    {
        using var container = TestContainer.Build(b =>
        {
            b.Register<IClock, Clock>().WithKey("red");
            b.Register<KeyInheritingClockUser>().WithKey("red");
        });

        var graph = container.BuildGraph();
        var user = graph.Nodes.Single(n => n.Key.Type == typeof(KeyInheritingClockUser));

        var edge = Assert.Single(graph.EdgesFrom(user));
        Assert.Equal(new ServiceKey(typeof(IClock), "red"), edge.To.Key);
    }

    [Fact]
    public void Generated_registrations_honour_the_Microsoft_attributes()
    {
        var fast = new Clock();
        var plain = new Clock();
        var builder = new ContainerBuilder();
        builder.RegisterInstance<IClock>(plain);
        builder.RegisterInstance<IClock>(fast).WithKey("fast");
        builder.RegisterGeneratedServices();
        using var container = builder.Build();

        var thing = container.Resolve<MicrosoftAttributedThing>("fast");

        Assert.Equal("fast", thing.Key);
        Assert.Same(fast, thing.SameKeyClock);
        Assert.Same(plain, thing.PlainClock);
    }

    [Fact]
    public void Through_the_adapter_failures_surface_as_InvalidOperationException_with_the_detail_inside()
    {
        var services = new ServiceCollection();
        services.AddSingleton<KeyedClockUser>();
        using var provider = (MaxenServiceProvider)services.BuildMaxenServiceProvider();

        var ex = Assert.Throws<InvalidOperationException>(() => provider.GetService(typeof(KeyedClockUser)));
        Assert.IsAssignableFrom<DIException>(ex.InnerException);
    }

    [Fact]
    public void Through_the_adapter_KeyedService_AnyKey_maps_to_Maxens_any_key()
    {
        var services = new ServiceCollection();
        services.AddKeyedSingleton<KeyAware>(KeyedService.AnyKey);
        using var provider = (MaxenServiceProvider)services.BuildMaxenServiceProvider();

        Assert.Equal("tenant-1", provider.GetRequiredKeyedService<KeyAware>("tenant-1").Key);
        Assert.Same(provider.GetRequiredKeyedService<KeyAware>("tenant-1"), provider.GetRequiredKeyedService<KeyAware>("tenant-1"));
        Assert.True(provider.GetRequiredService<IServiceProviderIsKeyedService>().IsKeyedService(typeof(KeyAware), "tenant-2"));
    }
}
