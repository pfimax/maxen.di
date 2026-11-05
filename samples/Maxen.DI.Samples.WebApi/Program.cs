using Maxen.DI;
using Maxen.DI.Extensibility;
using Maxen.DI.Extensions.AspNetCore;
using Maxen.DI.Samples.WebApi;

var builder = WebApplication.CreateBuilder(args);

// Ordinary ASP.NET registrations keep working...
builder.Services.AddSingleton<IGreeter, Greeter>();
builder.Services.AddScoped<WhoAmI>();

// A request's values (path, user, trace id) as a plain scoped service. Register it here, fill it with the middleware below.
builder.AddMaxenRequestValues();

// ...and Maxen-only features are available through the callback.
builder.UseMaxenDI(container =>
{
    container.Decorate<IGreeter, ExcitedGreeter>();

    // A module that is only applied when its feature is on: run with MAXEN_FEATURES=quiet to try it.
    container.RegisterModule("quiet", new QuietGreetingModule());
    container.UseFeaturesFromEnvironment();
});

var app = builder.Build();

app.UseMaxenRequestValues();

app.MapGet("/", (IGreeter greeter) => greeter.Greet("world"));

// WhoAmI takes RequestValues in its constructor, like any other dependency.
app.MapGet("/whoami", (WhoAmI who) => who.Describe());

// Each request has its own Maxen scope; this shows how to reach it for Maxen-specific calls.
app.MapGet("/scope", (HttpContext http) => http.GetMaxenScope() is not null ? "running on Maxen.DI" : "standard container");

// Registrations, dependencies and validation problems as JSON; 503 when validation fails, so it works as a readiness probe.
// In a real app, add .RequireAuthorization(): the output names your types.
app.MapMaxenDiagnostics();

app.Run();
