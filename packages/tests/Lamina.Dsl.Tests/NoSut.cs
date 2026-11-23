namespace Lamina.Dsl.Tests;

internal sealed class NoSut : ISystemUnderTest, ISutProvider
{
    public IReadOnlyDictionary<string, string> Metadata { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

    public Uri Endpoint(string name) => throw new NotSupportedException();

    public Task<ISystemUnderTest> StartAsync(CancellationToken ct) => Task.FromResult<ISystemUnderTest>(this);

    public Task WaitUntilReadyAsync(CancellationToken ct) => Task.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
