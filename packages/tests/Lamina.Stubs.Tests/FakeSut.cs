namespace Lamina.Stubs.Tests;

internal sealed class FakeSut(List<string> events) : ISystemUnderTest, ISutProvider
{
    public IReadOnlyDictionary<string, string> Metadata { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

    public Uri Endpoint(string name) => new($"http://{name}.test");

    public Task<ISystemUnderTest> StartAsync(CancellationToken ct) => Task.FromResult<ISystemUnderTest>(this);

    public Task WaitUntilReadyAsync(CancellationToken ct)
    {
        events.Add("sut ready");
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
