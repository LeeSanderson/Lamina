using Microsoft.Extensions.Configuration;

namespace Lamina.Drivers.Http;

/// <summary>
/// A spec's <see cref="HttpClient"/>s, one per SUT endpoint registered with
/// <see cref="LaminaBuilderHttpExtensions.AddHttpClient(LaminaBuilder, string)"/>.
/// </summary>
public sealed class SutHttpClients
{
    private readonly SpecScope _scope;
    private readonly ISystemUnderTest _sut;
    private readonly IHttpMessageHandlerFactory _handlers;
    private readonly HashSet<string> _endpoints;
    private readonly double _timeoutMultiplier;

    internal SutHttpClients(SpecScope scope, ISystemUnderTest sut, IHttpMessageHandlerFactory handlers, IEnumerable<HttpEndpoint> endpoints, IConfiguration configuration)
    {
        _scope = scope;
        _sut = sut;
        _handlers = handlers;
        _endpoints = endpoints.Select(e => e.Name).ToHashSet(StringComparer.Ordinal);
        _timeoutMultiplier = TimeoutMultiplier.Read(configuration);
    }

    /// <summary>
    /// Returns a client whose base address is the SUT's URI for <paramref name="endpointName"/>. Every request carries the spec's
    /// test ID as W3C baggage, and the timeout is scaled by <c>Lamina:TimeoutMultiplier</c>.
    /// </summary>
    public HttpClient For(string endpointName)
    {
        if (!_endpoints.Contains(endpointName))
        {
            throw new InvalidOperationException(
                $"No HTTP client is registered for endpoint '{endpointName}'. Call AddHttpClient(\"{endpointName}\") in the suite's Configure. " +
                $"Registered: {(_endpoints.Count == 0 ? "none" : string.Join(", ", _endpoints.Order(StringComparer.Ordinal)))}.");
        }

        // The factory's handler must outlive this client, so the client never disposes the handler chain.
        var client = new HttpClient(new SpecBaggageHandler(_scope, _handlers.CreateHandler(endpointName)), disposeHandler: false)
        {
            BaseAddress = _sut.Endpoint(endpointName),
        };
        client.Timeout *= _timeoutMultiplier;
        return client;
    }
}
