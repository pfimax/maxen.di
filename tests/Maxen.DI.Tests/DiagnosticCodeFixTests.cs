using Maxen.DI.CodeFixes;

namespace Maxen.DI.Tests;

/// <summary>Applies the quick fixes for MAXEN006 (missing registration) and MAXEN007 (disposable transient from the root).</summary>
public class DiagnosticCodeFixTests
{
    [Fact]
    public async Task A_missing_class_can_be_registered_from_the_quick_fix()
    {
        var fixedSource = await CaptiveLifetimeCodeFixTests.ApplyFix("""
            using Maxen.DI;
            public class Repo { }
            public class Service { public Service(Repo repo) { } }
            public static class Setup
            {
                public static void Configure(ContainerBuilder builder)
                {
                    builder.Register<Service>();
                }
            }
            """, "MAXEN006", provider: new MissingRegistrationCodeFix());

        Assert.Contains("builder.Register<Repo>();", fixedSource);
        Assert.Contains("builder.Register<Service>();", fixedSource);
        Assert.True(fixedSource.IndexOf("Register<Repo>", StringComparison.Ordinal) < fixedSource.IndexOf("Register<Service>", StringComparison.Ordinal));
    }

    [Fact]
    public async Task No_fix_is_offered_when_the_missing_service_is_an_interface()
    {
        // The fix cannot guess an implementation, so there must be no action at all.
        await Assert.ThrowsAnyAsync<Exception>(() => CaptiveLifetimeCodeFixTests.ApplyFix("""
            using Maxen.DI;
            public interface IRepo { }
            public class Service { public Service(IRepo repo) { } }
            public static class Setup
            {
                public static void Configure(ContainerBuilder builder) { builder.Register<Service>(); }
            }
            """, "MAXEN006", provider: new MissingRegistrationCodeFix()));
    }

    [Fact]
    public async Task A_root_resolve_can_be_moved_into_a_scope()
    {
        var fixedSource = await CaptiveLifetimeCodeFixTests.ApplyFix("""
            using Maxen.DI;
            public class Handle : System.IDisposable { public void Dispose() { } }
            public static class Setup
            {
                public static void Run(ContainerBuilder builder, Container container)
                {
                    builder.Register<Handle>();
                    var handle = container.Resolve<Handle>();
                }
            }
            """, "MAXEN007", provider: new RootResolveCodeFix());

        Assert.Contains("using var scope = container.CreateScope();", fixedSource);
        Assert.Contains("var handle = scope.Resolve<Handle>();", fixedSource);
    }

    [Fact]
    public async Task The_scope_name_avoids_names_already_in_use()
    {
        var fixedSource = await CaptiveLifetimeCodeFixTests.ApplyFix("""
            using Maxen.DI;
            public class Handle : System.IDisposable { public void Dispose() { } }
            public static class Setup
            {
                public static void Run(ContainerBuilder builder, Container container, int scope)
                {
                    builder.Register<Handle>();
                    var handle = container.Resolve<Handle>();
                }
            }
            """, "MAXEN007", provider: new RootResolveCodeFix());

        Assert.Contains("using var scope2 = container.CreateScope();", fixedSource);
        Assert.Contains("scope2.Resolve<Handle>()", fixedSource);
    }
}
