using System.Net;
using System.Text.Json;

namespace Folio.Payments;

internal sealed class PaymentProviderClient(HttpClient http)
{
    private const string PendingStatus = "pending";
    private const string IdempotencyKeyHeader = "Idempotency-Key";

    internal async Task<ProviderReply> CreatePaymentAsync(PaymentCreate payment, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("payments", UriKind.Relative))
            {
                Content = JsonContent.Create(new CreatePaymentRequest(payment.AmountInPence, "GBP", payment.CardToken, payment.Reference)),
            };
            request.Headers.Add(IdempotencyKeyHeader, payment.IdempotencyKey.ToString());
            using var response = await http.SendAsync(request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.PaymentRequired)
            {
                return ProviderReply.Declined;
            }

            if (response.IsSuccessStatusCode)
            {
                return await ReadAcceptedAsync(response, cancellationToken);
            }

            return ProviderReply.Unknown;
        }
        catch (Exception exception) when (IsLostAnswer(exception, cancellationToken))
        {
            return ProviderReply.Unknown;
        }
    }

    private static bool IsLostAnswer(Exception exception, CancellationToken cancellationToken) =>
        exception is HttpRequestException
        || (exception is TaskCanceledException && !cancellationToken.IsCancellationRequested);

    private static async Task<ProviderReply> ReadAcceptedAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var created = await response.Content.ReadFromJsonAsync<CreatedPayment>(cancellationToken);
            return created is { Id: { Length: > 0 } id, Status: PendingStatus }
                ? new ProviderReply(ProviderAnswer.Pending, id)
                : ProviderReply.Unknown;
        }
        catch (JsonException)
        {
            return ProviderReply.Unknown;
        }
    }

    private sealed record CreatePaymentRequest(long Amount, string Currency, string CardToken, string Reference);

    private sealed record CreatedPayment(string? Id, string? Status);
}

internal sealed record PaymentCreate(string Reference, Guid IdempotencyKey, long AmountInPence, string CardToken);

internal sealed record ProviderReply(ProviderAnswer Answer, string? ProviderPaymentId)
{
    internal static ProviderReply Unknown { get; } = new(ProviderAnswer.Unknown, ProviderPaymentId: null);

    internal static ProviderReply Declined { get; } = new(ProviderAnswer.Declined, ProviderPaymentId: null);
}

internal enum ProviderAnswer
{
    Pending,
    Declined,
    Unknown,
}

internal static class PaymentProviderRegistration
{
    internal static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

    internal static void AddPaymentProviderClient(this IHostApplicationBuilder builder)
    {
        var timeout = builder.Configuration.GetValue<TimeSpan?>("PaymentProvider:Timeout") ?? DefaultTimeout;
        var client = builder.Services.AddHttpClient<PaymentProviderClient>(http =>
        {
            http.BaseAddress = new Uri(builder.Configuration.GetRequiredSection("Clients:PaymentProvider").Value!);
            http.Timeout = timeout;
        });

        // A lost answer must reach the Payments retry worker, which resubmits with the same Idempotency-Key.
        // The standard resilience handler would retry the create itself and hide that (#17).
#pragma warning disable EXTEXP0001 // RemoveAllResilienceHandlers is experimental; it is the only way to opt one client out of ConfigureHttpClientDefaults.
        client.RemoveAllResilienceHandlers();
#pragma warning restore EXTEXP0001
    }
}
