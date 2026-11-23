namespace Lamina.Sut.Deployed.Tests;

public sealed class ReadinessTests
{
    [Fact]
    public async Task TheRunStartsOnceEveryReadinessPathAnswers2xx()
    {
        await using var bff = await FakeService.StartAsync(failuresBeforeReady: 3);
        await using var stub = await FakeService.StartAsync(failuresBeforeReady: 1);

        await using var run = await DeployedSuite.StartAsync(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Lamina:Sut:Deployed:Endpoints:web-bff"] = bff.BaseAddress.ToString(),
            ["Lamina:Sut:Deployed:Endpoints:payment-provider"] = stub.BaseAddress.ToString(),
            ["Lamina:Sut:Deployed:Readiness:web-bff"] = "/health",
            ["Lamina:Sut:Deployed:Readiness:payment-provider"] = "/__admin/health",
        });

        bff.Requests.ShouldBe(["/health", "/health", "/health", "/health"]);
        stub.Requests.ShouldBe(["/__admin/health", "/__admin/health"]);
    }

    [Fact]
    public async Task AnEndpointWithNoReadinessPathIsNotChecked()
    {
        await using var bff = await FakeService.StartAsync(failuresBeforeReady: 1000);

        await using var run = await DeployedSuite.StartAsync(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Lamina:Sut:Deployed:Endpoints:web-bff"] = bff.BaseAddress.ToString(),
        });

        bff.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task AReadinessPathThatNeverAnswers2xxFailsTheRunNamingTheEndpoint()
    {
        await using var bff = await FakeService.StartAsync(failuresBeforeReady: 1000);
        await using var stub = await FakeService.StartAsync();

        var error = await Should.ThrowAsync<TimeoutException>(() => DeployedSuite.StartAsync(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Lamina:Sut:ReadyTimeout"] = "00:00:01",
            ["Lamina:Sut:Deployed:Endpoints:web-bff"] = bff.BaseAddress.ToString(),
            ["Lamina:Sut:Deployed:Endpoints:payment-provider"] = stub.BaseAddress.ToString(),
            ["Lamina:Sut:Deployed:Readiness:web-bff"] = "/health",
            ["Lamina:Sut:Deployed:Readiness:payment-provider"] = "/__admin/health",
        }));

        error.Message.ShouldBe(
            $"The deployed SUT was not ready within 00:00:01. Not ready:{Environment.NewLine}- web-bff: {new Uri(bff.BaseAddress, "/health")} answered 503 (Service Unavailable)");
    }

    [Fact]
    public async Task TheReadyTimeoutIsScaledByTheTimeoutMultiplier()
    {
        await using var bff = await FakeService.StartAsync(failuresBeforeReady: 1000);

        var error = await Should.ThrowAsync<TimeoutException>(() => DeployedSuite.StartAsync(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Lamina:Sut:ReadyTimeout"] = "00:00:01",
            ["Lamina:TimeoutMultiplier"] = "1.5",
            ["Lamina:Sut:Deployed:Endpoints:web-bff"] = bff.BaseAddress.ToString(),
            ["Lamina:Sut:Deployed:Readiness:web-bff"] = "/health",
        }));

        error.Message.ShouldStartWith("The deployed SUT was not ready within 00:00:01.5000000.");
    }

    [Fact]
    public async Task AnEndpointThatRefusesConnectionsIsReportedWithTheError()
    {
        var closed = await ClosedAddressAsync();

        var error = await Should.ThrowAsync<TimeoutException>(() => DeployedSuite.StartAsync(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Lamina:Sut:ReadyTimeout"] = "00:00:05",
            ["Lamina:Sut:Deployed:Endpoints:web-bff"] = closed.ToString(),
            ["Lamina:Sut:Deployed:Readiness:web-bff"] = "/health",
        }));

        error.Message.ShouldStartWith($"The deployed SUT was not ready within 00:00:05. Not ready:{Environment.NewLine}- web-bff: {new Uri(closed, "/health")} failed: ");
    }

    [Fact]
    public async Task TeardownLeavesTheSystemAlone()
    {
        await using var bff = await FakeService.StartAsync();
        var run = await DeployedSuite.StartAsync(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Lamina:Sut:Deployed:Endpoints:web-bff"] = bff.BaseAddress.ToString(),
            ["Lamina:Sut:Deployed:Readiness:web-bff"] = "/health",
        });
        var requestsBeforeTeardown = bff.Requests.Count;

        await run.DisposeAsync();

        bff.Requests.Count.ShouldBe(requestsBeforeTeardown);
        run.Sut.Endpoint("web-bff").ShouldBe(bff.BaseAddress);
    }

    private static async Task<Uri> ClosedAddressAsync()
    {
        await using var service = await FakeService.StartAsync();
        return service.BaseAddress;
    }
}
