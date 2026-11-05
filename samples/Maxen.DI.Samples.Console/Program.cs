using Maxen.DI.Diagnostics;
using Maxen.DI;
using Maxen.DI.Samples.Console;

// A tour of the main ideas. Run it normally, then again with MAXEN_PROFILE=Development to swap the mailer.

var builder = new ContainerBuilder();

builder.Register<IMailer, SmtpMailer>().AsSingleton();
builder.Decorate<IMailer, LoggingMailer>();                       // wraps whatever IMailer ends up being
builder.Register<RequestContext>().AsScopedTo("request");         // one per "request" scope
builder.RegisterGeneratedServices();                              // [Service], [Decorator] and [Intercept] classes, no reflection

// Profiles replace registrations without touching the defaults above.
builder.DefineProfile("Development", p => p.Replace<IMailer, ConsoleMailer>());
builder.UseProfileFromEnvironment();

// Record what gets built, with timings.
var trace = new ResolutionTrace();
builder.Observe(trace);

await using var container = builder.Build();

var mailer = container.Resolve<IMailer>();
mailer.Send("ada@example.com", "hello from Maxen.DI");

// Nested scopes inside a labelled scope share its instance.
using var request = container.CreateScope("request");
using var step = request.CreateScope();
Console.WriteLine($"same request context in nested scope: {ReferenceEquals(request.Resolve<RequestContext>(), step.Resolve<RequestContext>())}");

// Mistakes come with hints.
try
{
    container.Resolve<SmtpMailer>();
}
catch (ServiceNotRegisteredException ex)
{
    Console.WriteLine(ex.Message);
}

// Attribute-driven: the greeting is decorated and intercepted without a single builder call for either.
var greeting = container.Resolve<IGreeting>();
Console.WriteLine($"{greeting.For("Ada")} (intercepted calls: {CountingInterceptor.Calls})");

// Telemetry: Maxen.DI publishes activities and metrics under the name "Maxen.DI"; any listener or OpenTelemetry can read them.
var spans = 0;
using var listener = new System.Diagnostics.ActivityListener
{
    ShouldListenTo = source => source.Name == MaxenTelemetry.Name,
    Sample = (ref System.Diagnostics.ActivityCreationOptions<System.Diagnostics.ActivityContext> _) => System.Diagnostics.ActivitySamplingResult.AllData,
    ActivityStopped = _ => spans++,
};
System.Diagnostics.ActivitySource.AddActivityListener(listener);

var traced = new ContainerBuilder();
traced.UseTelemetry();
traced.Register<IMailer, ConsoleMailer>().AsSingleton();
await using var tracedContainer = traced.Build();
tracedContainer.Resolve<IMailer>();
Console.WriteLine($"telemetry spans recorded for one resolve: {spans}");

Console.WriteLine();
Console.WriteLine("What was built:");
Console.Write(trace);
