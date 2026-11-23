namespace Lamina;

/// <summary>
/// The system a suite's specs exercise, seen from outside through its named endpoints.
/// </summary>
public interface ISystemUnderTest : IAsyncDisposable
{
    IReadOnlyDictionary<string, string> Metadata { get; }

    Uri Endpoint(string name);

    Task WaitUntilReadyAsync(CancellationToken ct);
}
