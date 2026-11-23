namespace Lamina;

// The run owns the SUT and disposes it after the container, so the container must not dispose it too.
internal sealed class BorrowedSut(ISystemUnderTest inner) : ISystemUnderTest
{
    public IReadOnlyDictionary<string, string> Metadata => inner.Metadata;

    public Uri Endpoint(string name) => inner.Endpoint(name);

    public Task WaitUntilReadyAsync(CancellationToken ct) => inner.WaitUntilReadyAsync(ct);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
