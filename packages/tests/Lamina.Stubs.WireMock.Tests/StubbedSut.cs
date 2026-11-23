namespace Lamina.Stubs.WireMock.Tests;

internal sealed class StubbedSut(Uri paymentProvider) : ISystemUnderTest, ISutProvider
{
    public IReadOnlyDictionary<string, string> Metadata { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

    public Uri Endpoint(string name) =>
        name == "payment-provider" ? paymentProvider : throw new KeyNotFoundException($"No endpoint '{name}'.");

    public Task<ISystemUnderTest> StartAsync(CancellationToken ct) => Task.FromResult<ISystemUnderTest>(this);

    public Task WaitUntilReadyAsync(CancellationToken ct) => Task.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
