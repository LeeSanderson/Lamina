namespace Lamina;

/// <summary>
/// The system a suite's specs exercise, seen from outside through its named endpoints.
/// </summary>
public interface ISystemUnderTest : IAsyncDisposable
{
    IReadOnlyDictionary<string, string> Metadata { get; }

    /// <summary>
    /// Gets the names <see cref="Endpoint"/> resolves, for the run's start-up diagnostics. When it lists any, the run fails at
    /// start-up if an endpoint the suite requires (<see cref="LaminaBuilder.RequireEndpoint(string)"/>) isn't among them.
    /// </summary>
    IReadOnlyCollection<string> EndpointNames => [];

    Uri Endpoint(string name);

    Task WaitUntilReadyAsync(CancellationToken ct);
}
