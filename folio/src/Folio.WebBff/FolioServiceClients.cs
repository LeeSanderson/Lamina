using Folio.ServiceDefaults;

namespace Folio.WebBff;

internal static class FolioServiceClients
{
    internal const string SubjectHeader = "X-Folio-Subject";

    internal static void AddFolioServiceClients(this WebApplicationBuilder builder)
    {
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddTransient<SubjectForwardingHandler>();
        builder.Services.AddHttpClient<CatalogueClient>(client => client.BaseAddress = builder.Configuration.ClientAddress("Catalogue"))
            .AddHttpMessageHandler<SubjectForwardingHandler>();
        builder.Services.AddHttpClient<OrderingClient>(client => client.BaseAddress = builder.Configuration.ClientAddress("Ordering"))
            .AddHttpMessageHandler<SubjectForwardingHandler>();
    }

    private sealed class SubjectForwardingHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var subject = httpContextAccessor.HttpContext?.User.FindFirst(TestTokenAuthentication.SubjectClaim)?.Value;
            if (subject is not null)
            {
                request.Headers.Add(SubjectHeader, subject);
            }

            return base.SendAsync(request, cancellationToken);
        }
    }
}
