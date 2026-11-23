using Microsoft.Extensions.DependencyInjection;

namespace Lamina.Tests;

public sealed class RunStartTests
{
    [Fact]
    public async Task HooksRunInRegistrationOrderAfterReadinessAndBeforeAnySpec()
    {
        var suite = new TestSuite(b => b.Services
            .AddSingleton<IRunStartHook>(sp => new RecordingHook("first", Events(sp)))
            .AddSingleton<IRunStartHook>(sp => new RecordingHook("second", Events(sp))));

        await using (var run = await suite.StartAsync(TestContext.Current.CancellationToken))
        {
            await using var scope = run.CreateScope(channel: null, specName: "spec");
            suite.Events.Add("spec");
        }

        suite.Events.ShouldBe(
        [
            "sut started",
            "sut ready",
            "first ran",
            "second ran",
            "spec",
            "second disposed",
            "first disposed",
            "sut disposed",
        ]);
    }

    [Fact]
    public async Task HooksAreGivenTheStartedSut()
    {
        ISystemUnderTest? seen = null;
        var suite = new TestSuite(b => b.Services.AddSingleton<IRunStartHook>(new CapturingHook(sut => seen = sut)));

        await using var run = await suite.StartAsync(TestContext.Current.CancellationToken);

        seen.ShouldNotBeNull().Endpoint("web-bff").ShouldBe(new Uri("http://web-bff.test"));
    }

    [Fact]
    public async Task TheSutIsInjectableIntoRunServices()
    {
        await using var run = await TestSuite.StartAsync();
        await using var scope = run.CreateScope(channel: null, specName: "spec");

        scope.Services.GetRequiredService<ISystemUnderTest>().Endpoint("payments").ShouldBe(new Uri("http://payments.test"));
    }

    [Fact]
    public async Task TheSpecScopeIsInjectableWithinItsOwnServices()
    {
        await using var run = await TestSuite.StartAsync();
        await using var scope = run.CreateScope(channel: null, specName: "spec");

        scope.Services.GetRequiredService<SpecScope>().ShouldBeSameAs(scope);
    }

    [Fact]
    public async Task ARequiredEndpointTheSutDoesNotHaveFailsTheRunBeforeReadiness()
    {
        var suite = new TestSuite(b => b.RequireEndpoint("web-bff").RequireEndpoint("payments").RequireEndpoint("payment-provider"))
        {
            Endpoints = ["web-bff", "catalogue"],
        };

        var error = await Should.ThrowAsync<InvalidOperationException>(() => suite.StartAsync(TestContext.Current.CancellationToken));

        error.Message.ShouldBe(
            "The suite needs SUT endpoints that SUT provider 'fake' doesn't have: payments, payment-provider. " +
            "It has: web-bff, catalogue.");
        suite.Events.ShouldBe(["sut started", "sut disposed"]);
    }

    [Fact]
    public async Task AnEndpointRequiredTwiceIsCheckedOnce()
    {
        var suite = new TestSuite(b => b.RequireEndpoint("payments").RequireEndpoint("payments")) { Endpoints = ["web-bff"] };

        var error = await Should.ThrowAsync<InvalidOperationException>(() => suite.StartAsync(TestContext.Current.CancellationToken));

        error.Message.ShouldStartWith("The suite needs SUT endpoints that SUT provider 'fake' doesn't have: payments. ");
    }

    [Fact]
    public async Task ARequiredEndpointCanBeNamedFromTheRunsServices()
    {
        var suite = new TestSuite(b =>
        {
            b.Services.AddSingleton(new EndpointName("payments"));
            b.RequireEndpoint(sp => sp.GetRequiredService<EndpointName>().Value);
        })
        {
            Endpoints = ["web-bff"],
        };

        var error = await Should.ThrowAsync<InvalidOperationException>(() => suite.StartAsync(TestContext.Current.CancellationToken));

        error.Message.ShouldStartWith("The suite needs SUT endpoints that SUT provider 'fake' doesn't have: payments. ");
    }

    [Fact]
    public async Task RequiredEndpointsAreNotCheckedAgainstASutThatListsNoEndpoints()
    {
        var suite = new TestSuite(b => b.RequireEndpoint("payments"));

        await using var run = await suite.StartAsync(TestContext.Current.CancellationToken);

        run.Sut.Endpoint("payments").ShouldBe(new Uri("http://payments.test"));
    }

    private sealed record EndpointName(string Value);

    private static List<string> Events(IServiceProvider services) => services.GetRequiredService<List<string>>();

    private sealed class RecordingHook(string name, List<string> events) : IRunStartHook, IAsyncDisposable
    {
        public Task OnRunStartAsync(ISystemUnderTest sut, CancellationToken ct)
        {
            events.Add($"{name} ran");
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            events.Add($"{name} disposed");
            return ValueTask.CompletedTask;
        }
    }

    private sealed class CapturingHook(Action<ISystemUnderTest> capture) : IRunStartHook
    {
        public Task OnRunStartAsync(ISystemUnderTest sut, CancellationToken ct)
        {
            capture(sut);
            return Task.CompletedTask;
        }
    }
}
