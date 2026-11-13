using Maxen.DI.Extensions.AspNetCore;
using Maxen.DI.Tests.Fixtures;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Maxen.DI.Tests;

/// <summary>Real HTTP requests against a real Kestrel server running on Maxen.DI.</summary>
public class AspNetCoreEndToEndTests
{
    // Starts a web app on a free local port, runs the test body, and shuts everything down again.
    private static async Task WithApp(
        Action<WebApplicationBuilder> configure,
        Action<WebApplication> map,
        Func<HttpClient, WebApplication, Task> test,
        Action<ContainerBuilder>? container = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        configure(builder);

        // A second UseMaxenDI call would replace the first, so Maxen-only settings come in through here.
        builder.UseMaxenDI(container);

        await using var app = builder.Build();
        map(app);
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };
        try { await test(client, app); }
        finally { await app.StopAsync(); }
    }

    [Fact]
    public async Task A_request_shares_one_scoped_instance_between_middleware_and_endpoint()
    {
        await WithApp(
            b => b.Services.AddScoped<RequestId>(),
            app =>
            {
                // The middleware stashes its view of the scoped service; the endpoint must see the very same object.
                app.Use(async (context, next) =>
                {
                    context.Items["fromMiddleware"] = context.RequestServices.GetRequiredService<RequestId>().Value;
                    await next();
                });
                app.MapGet("/id", (RequestId id, HttpContext context) =>
                    $"{id.Value}|{context.Items["fromMiddleware"]}");
            },
            async (client, _) =>
            {
                var first = (await client.GetStringAsync("/id")).Split('|');
                var second = (await client.GetStringAsync("/id")).Split('|');

                Assert.Equal(first[0], first[1]);
                Assert.Equal(second[0], second[1]);
                Assert.NotEqual(first[0], second[0]);
            });
    }

    [Fact]
    public async Task Controllers_are_built_by_the_container()
    {
        await WithApp(
            b =>
            {
                b.Services.AddScoped<RequestId>();
                b.Services.AddControllers().AddApplicationPart(typeof(PingController).Assembly);
            },
            app => app.MapControllers(),
            async (client, _) =>
            {
                var first = await client.GetStringAsync("/ping");
                var second = await client.GetStringAsync("/ping");

                Assert.True(Guid.TryParse(first, out var parsed));
                Assert.NotEqual(Guid.Empty, parsed);
                Assert.NotEqual(first, second);
            });
    }

    [Fact]
    public async Task Hosted_services_are_built_with_their_dependencies_and_started_and_stopped()
    {
        var state = new ProbeState();
        await WithApp(
            b =>
            {
                b.Services.AddSingleton<IClock, Clock>();
                b.Services.AddSingleton(state);
                b.Services.AddHostedService<StartupProbe>();
            },
            _ => { },
            (_, _) =>
            {
                Assert.True(state.Started);
                return Task.CompletedTask;
            });

        Assert.True(state.Stopped);
    }

    [Fact]
    public async Task Keyed_services_can_be_asked_for_by_key_in_minimal_APIs()
    {
        await WithApp(
            b => b.Services.AddKeyedSingleton<IGreeter, LoudGreeterWithoutDependencies>("loud"),
            app => app.MapGet("/greet", ([FromKeyedServices("loud")] IGreeter greeter) => greeter.Greet()),
            async (client, _) => Assert.Equal("HELLO", await client.GetStringAsync("/greet")));
    }

    [Fact]
    public async Task The_diagnostics_endpoint_lists_registrations_and_reports_a_sound_container()
    {
        await WithApp(
            b => b.Services.AddSingleton<IClock, Clock>(),
            app => app.MapMaxenDiagnostics(),
            async (client, _) =>
            {
                var response = await client.GetAsync("/_maxen");
                var text = await response.Content.ReadAsStringAsync();

                Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
                using var json = System.Text.Json.JsonDocument.Parse(text);
                Assert.True(json.RootElement.GetProperty("healthy").GetBoolean());
                Assert.Contains("Clock", text);
            });
    }

    [Fact]
    public async Task The_diagnostics_endpoint_answers_503_when_validation_finds_a_problem()
    {
        await WithApp(
            // Greeter takes an IClock that is never registered.
            b => b.Services.AddSingleton<IGreeter, Greeter>(),
            app => app.MapMaxenDiagnostics("/health/di"),
            async (client, _) =>
            {
                var response = await client.GetAsync("/health/di");

                Assert.Equal(System.Net.HttpStatusCode.ServiceUnavailable, response.StatusCode);
                Assert.Contains("MissingDependency", await response.Content.ReadAsStringAsync());
            });
    }

    [Fact]
    public async Task The_diagnostics_endpoint_validates_once_so_factories_do_not_run_on_every_probe()
    {
        var factoryRuns = 0;
        await WithApp(
            // Transient, so every validation run would call the factory again.
            b => b.Services.AddTransient<IClock>(_ =>
            {
                Interlocked.Increment(ref factoryRuns);
                return new Clock();
            }),
            app => app.MapMaxenDiagnostics(),
            async (client, _) =>
            {
                for (var i = 0; i < 3; i++)
                    Assert.Equal(System.Net.HttpStatusCode.OK, (await client.GetAsync("/_maxen")).StatusCode);

                Assert.Equal(1, Volatile.Read(ref factoryRuns));
            },
            c => c.Options.ValidateFactories = true);
    }

    [Fact]
    public async Task The_diagnostics_endpoint_tries_again_after_a_request_that_failed()
    {
        await WithApp(
            b => b.Services.AddTransient<NeedsFlaky>(),
            app => app.MapMaxenDiagnostics(),
            async (client, _) =>
            {
                // The source throws the first time validation asks it, and works from then on. The failure must not
                // be remembered, or a readiness probe would keep failing until the app restarts.
                Assert.Equal(System.Net.HttpStatusCode.InternalServerError, (await client.GetAsync("/_maxen")).StatusCode);
                Assert.Equal(System.Net.HttpStatusCode.OK, (await client.GetAsync("/_maxen")).StatusCode);
            },
            c => c.AddSource(new FlakySource()));
    }

    [Fact]
    public async Task Request_values_reach_services_built_by_the_container()
    {
        await WithApp(
            b =>
            {
                b.AddMaxenRequestValues();
                b.Services.AddScoped<PathReporter>();
            },
            app =>
            {
                app.UseMaxenRequestValues();
                app.MapGet("/where", (PathReporter reporter) => reporter.Describe());
            },
            async (client, _) => Assert.Equal("/where", await client.GetStringAsync("/where")));
    }

    [Fact]
    public void Request_values_explain_themselves_when_used_outside_a_request()
    {
        var values = new RequestValues();

        Assert.False(values.IsAttached);
        Assert.Contains("UseMaxenRequestValues", Assert.Throws<InvalidOperationException>(() => values.HttpContext).Message);
    }

    private sealed class PathReporter
    {
        private readonly RequestValues _request;

        public PathReporter(RequestValues request) => _request = request;

        public string Describe() => _request.Request.Path.Value ?? string.Empty;
    }

    [Fact]
    public async Task Endpoint_filters_can_resolve_services_from_the_request()
    {
        await WithApp(
            b => b.Services.AddScoped<RequestId>(),
            app => app.MapGet("/filtered", (RequestId id) => id.Value.ToString())
                .AddEndpointFilter(async (context, next) =>
                {
                    // The filter's view of the scoped service must be the endpoint's too.
                    var seen = context.HttpContext.RequestServices.GetRequiredService<RequestId>().Value.ToString();
                    var result = await next(context);
                    return seen == result?.ToString() ? "same" : "different";
                }),
            async (client, _) => Assert.Equal("same", await client.GetStringAsync("/filtered")));
    }
}
