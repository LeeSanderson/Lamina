namespace Lamina;

/// <summary>
/// Run-level set-up that happens once, after the system under test is ready and before any spec.
/// </summary>
public interface IRunStartHook
{
    Task OnRunStartAsync(ISystemUnderTest sut, CancellationToken ct);
}
