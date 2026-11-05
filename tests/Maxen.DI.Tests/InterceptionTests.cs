using Maxen.DI.Interception;
using Maxen.DI.Tests.Fixtures;

namespace Maxen.DI.Tests;

public class InterceptionTests
{
    [Fact]
    public void An_interceptor_sees_calls_and_can_change_results()
    {
        var recorder = new CallRecorder();
        using var container = TestContainer.Build(b =>
        {
            b.Register<IClock, Clock>();
            b.Register<IGreeter, Greeter>().InterceptWith(recorder).InterceptWith(new ShoutInterceptor());
        });

        var greeter = container.Resolve<IGreeter>();

        // The real greeter says "hello"; the interceptor adds the exclamation mark.
        Assert.Equal("hello!", greeter.Greet());
        Assert.Equal(new[] { "Greet" }, recorder.Calls);
    }

    [Fact]
    public void Interceptors_can_come_from_the_container()
    {
        using var container = TestContainer.Build(b =>
        {
            b.Register<IClock, Clock>();
            b.Register<IGreeter, Greeter>();
            b.Register<ShoutInterceptor>().AsSingleton();
            b.Intercept<IGreeter, ShoutInterceptor>();
        });

        Assert.Equal("hello!", container.Resolve<IGreeter>().Greet());
    }

    [Fact]
    public void Exceptions_from_the_service_are_not_wrapped()
    {
        using var container = TestContainer.Build(b =>
            b.Register<IThrower, Thrower>().InterceptWith(new CallRecorder()));

        Assert.Throws<InvalidOperationException>(() => container.Resolve<IThrower>().Boom());
    }

    [Fact]
    public void Only_interfaces_can_be_intercepted()
    {
        var builder = new ContainerBuilder();

        Assert.Throws<RegistrationException>(() => builder.Intercept<Clock, CallRecorder>());
    }
}
