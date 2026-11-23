namespace Lamina.Tests;

public sealed class RunDiagnosticsTests
{
    [Fact]
    public async Task TheStartLineGivesTheProviderEachEndpointWithItsUrlTheMetadataAndTheTimeToReady()
    {
        await using var run = await TestSuite.StartAsync(b =>
        {
            b.Configuration["Lamina:Sut:Provider"] = "described";
            b.UseSut(sut => sut.AddProvider("described", _ => new DescribedSut()));
        });

        run.StartLine.ShouldMatch(
            @"^Lamina run started with SUT provider 'described', ready in \d+\.\d s\. " +
            @"Endpoints: web-bff = http://web-bff\.test/, payments = http://payments\.test/\. " +
            @"Metadata: apphost = Folio\.AppHost, arguments = --Profile acceptance\.$");
    }

    [Fact]
    public async Task TheStartLineSaysWhenTheSutHasNoEndpointsOrMetadata()
    {
        await using var run = await TestSuite.StartAsync();

        run.StartLine.ShouldMatch(@"^Lamina run started with SUT provider 'fake', ready in \d+\.\d s\. Endpoints: none\. Metadata: none\.$");
    }

    [Fact]
    public async Task DisposingTheRunReportsHowLongTeardownTook()
    {
        var run = await TestSuite.StartAsync();
        run.TeardownLine.ShouldBeNull();

        await run.DisposeAsync();

        run.TeardownLine.ShouldNotBeNull().ShouldMatch(@"^Lamina run torn down in \d+\.\d s\.$");
    }

    private sealed class DescribedSut : ISystemUnderTest, ISutProvider
    {
        public IReadOnlyDictionary<string, string> Metadata { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["apphost"] = "Folio.AppHost",
            ["arguments"] = "--Profile acceptance",
        };

        public IReadOnlyCollection<string> EndpointNames { get; } = ["web-bff", "payments"];

        public Uri Endpoint(string name) => new($"http://{name}.test");

        public Task<ISystemUnderTest> StartAsync(CancellationToken ct) => Task.FromResult<ISystemUnderTest>(this);

        public Task WaitUntilReadyAsync(CancellationToken ct) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
