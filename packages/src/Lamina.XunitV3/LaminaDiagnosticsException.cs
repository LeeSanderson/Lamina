namespace Lamina.XunitV3;

/// <summary>
/// Carries a failing spec's footer (test ID, channel and aliases) into its failure message. Never thrown for a passing spec.
/// </summary>
public sealed class LaminaDiagnosticsException : Exception
{
    public LaminaDiagnosticsException()
    {
    }

    public LaminaDiagnosticsException(string message)
        : base(message)
    {
    }

    public LaminaDiagnosticsException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
