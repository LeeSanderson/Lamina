namespace Lamina;

/// <summary>
/// The system a suite's specs exercise, seen from outside through its named endpoints.
/// </summary>
public interface ISystemUnderTest : IAsyncDisposable
{
    IReadOnlyDictionary<string, string> Metadata { get; }

    /// <summary>
    /// Gets the names <see cref="Endpoint"/> resolves, for the run's start-up diagnostics.
    /// </summary>
    IReadOnlyCollection<string> EndpointNames => [];

    Uri Endpoint(string name);

    Task WaitUntilReadyAsync(CancellationToken ct);
}
