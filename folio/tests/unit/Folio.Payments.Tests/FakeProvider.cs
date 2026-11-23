using Folio.ServiceDefaults;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Folio.Payments.Tests;

internal sealed class FakeProvider : IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly AnsweringHandler _handler;

    private FakeProvider(WebApplication app, AnsweringHandler handler)
    {
        _app = app;
        _handler = handler;
    }

    internal PaymentProviderClient Client => _app.Services.GetRequiredService<PaymentProviderClient>();

    internal IReadOnlyList<HttpRequestMessage> Requests => _handler.Requests;

    internal IReadOnlyList<string> RequestBodies => _handler.RequestBodies;

    internal static FakeProvider Answering(Func<CancellationToken, Task<HttpResponseMessage>> answer) =>
        Answering(answer, providerTimeout: null);

    internal static FakeProvider Answering(Func<CancellationToken, HttpResponseMessage> answer) =>
        Answering(cancellationToken => Task.FromResult(answer(cancellationToken)), providerTimeout: null);

    internal static FakeProvider Answering(Func<CancellationToken, Task<HttpResponseMessage>> answer, TimeSpan? providerTimeout)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration["Clients:PaymentProvider"] = "http://payment-provider";
        if (providerTimeout is not null)
        {
            builder.Configuration["PaymentProvider:Timeout"] = providerTimeout.Value.ToString("c", System.Globalization.CultureInfo.InvariantCulture);
        }

        builder.AddServiceDefaults();
        builder.AddPaymentProviderClient();
        var handler = new AnsweringHandler(answer);
        builder.Services.AddHttpClient<PaymentProviderClient>().ConfigurePrimaryHttpMessageHandler(() => handler);
        return new FakeProvider(builder.Build(), handler);
    }

    public ValueTask DisposeAsync() => _app.DisposeAsync();

    private sealed class AnsweringHandler(Func<CancellationToken, Task<HttpResponseMessage>> answer) : HttpMessageHandler
    {
        private readonly List<HttpRequestMessage> _requests = [];
        private readonly List<string> _requestBodies = [];

        internal IReadOnlyList<HttpRequestMessage> Requests => _requests;

        internal IReadOnlyList<string> RequestBodies => _requestBodies;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _requests.Add(request);
            _requestBodies.Add(request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken));
            return await answer(cancellationToken);
        }
    }
}
