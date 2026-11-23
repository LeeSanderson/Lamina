namespace Lamina;

/// <summary>
/// Thrown when <see cref="Eventually"/> gives up. The message names the expectation, the time allowed and what was observed.
/// </summary>
public sealed class EventuallyFailedException : Exception
{
    public EventuallyFailedException()
    {
    }

    public EventuallyFailedException(string message)
        : base(message)
    {
    }

    public EventuallyFailedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
