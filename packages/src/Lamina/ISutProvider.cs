namespace Lamina;

/// <summary>
/// Starts or locates the system under test once per run.
/// </summary>
public interface ISutProvider
{
    Task<ISystemUnderTest> StartAsync(CancellationToken ct);
}
