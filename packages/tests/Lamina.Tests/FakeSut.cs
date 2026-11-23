namespace Lamina.Tests;

internal sealed class FakeSut(List<string> events) : ISystemUnderTest, ISutProvider
{
    public IReadOnlyDictionary<string, string> Metadata { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

    public Uri Endpoint(string name) => new($"http://{name}.test");

    public Task<ISystemUnderTest> StartAsync(CancellationToken ct)
    {
        events.Add("sut started");
        return Task.FromResult<ISystemUnderTest>(this);
    }

    public Task WaitUntilReadyAsync(CancellationToken ct)
    {
        events.Add("sut ready");
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        events.Add("sut disposed");
        return ValueTask.CompletedTask;
    }
}
