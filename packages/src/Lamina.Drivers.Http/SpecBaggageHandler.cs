using System.Diagnostics;

namespace Lamina.Drivers.Http;

// Puts the spec's test ID in the baggage of the current Activity for the length of the request. HttpClient's own
// diagnostics handler then writes the baggage header through .NET's default W3C propagator, so no Lamina code formats it.
internal sealed class SpecBaggageHandler(SpecScope scope, HttpMessageHandler inner) : DelegatingHandler(inner)
{
    internal const string TestIdKey = "lamina.test";

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var activity = new Activity("Lamina.Spec").SetBaggage(TestIdKey, scope.TestId).Start();
        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
