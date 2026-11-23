namespace Lamina.XunitV3.Tests.Samples;

internal sealed class FakeSut : ISystemUnderTest, ISutProvider
{
    public IReadOnlyDictionary<string, string> Metadata { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

    public Uri Endpoint(string name) => new($"http://{name}.test");

    public Task<ISystemUnderTest> StartAsync(CancellationToken ct) => Task.FromResult<ISystemUnderTest>(this);

    public Task WaitUntilReadyAsync(CancellationToken ct) => Task.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
