using System.Net;

namespace Lamina.Drivers.Http;

/// <summary>
/// A response that wasn't the success a driver expected. Holds its status, and its problem details when it had them.
/// </summary>
public sealed class HttpProblemException : HttpRequestException
{
    public HttpProblemException()
    {
    }

    public HttpProblemException(string message)
        : base(message)
    {
    }

    public HttpProblemException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public HttpProblemException(string message, HttpStatusCode statusCode, HttpProblem? problem)
        : base(message, inner: null, statusCode)
    {
        Problem = problem;
    }

    public HttpProblem? Problem { get; }
}
