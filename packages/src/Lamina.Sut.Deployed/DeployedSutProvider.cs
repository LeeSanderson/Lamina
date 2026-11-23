using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace Lamina.Sut.Deployed;

internal sealed class DeployedSutProvider(IConfiguration configuration) : ISutProvider
{
    private const string Section = "Lamina:Sut:Deployed";
    private const string ReadyTimeoutKey = "Lamina:Sut:ReadyTimeout";
    private const string TimeoutMultiplierKey = "Lamina:TimeoutMultiplier";
    private static readonly TimeSpan DefaultReadyTimeout = TimeSpan.FromMinutes(1);

    public Task<ISystemUnderTest> StartAsync(CancellationToken ct)
    {
        var deployed = configuration.GetSection(Section);
        var endpoints = deployed.GetSection("Endpoints").GetChildren()
            .ToDictionary(e => e.Key, ParseEndpoint, StringComparer.Ordinal);
        if (endpoints.Count == 0)
        {
            throw new InvalidOperationException(
                $"Lamina:Sut:Provider is 'deployed', but {Section}:Endpoints has no endpoints. Add a URL for each endpoint the suite uses, such as {Section}:Endpoints:web-bff.");
        }

        var readiness = deployed.GetSection("Readiness").GetChildren()
            .ToDictionary(r => r.Key, r => r.Value ?? string.Empty, StringComparer.Ordinal);
        if (readiness.Keys.FirstOrDefault(name => !endpoints.ContainsKey(name)) is { } name)
        {
            throw new InvalidOperationException(
                $"{Section}:Readiness:{name} has no matching endpoint. Add {Section}:Endpoints:{name}. Known: {string.Join(", ", endpoints.Keys.Order(StringComparer.Ordinal))}.");
        }

        var metadata = new Dictionary<string, string>(StringComparer.Ordinal) { ["provider"] = "deployed" };
        foreach (var entry in deployed.GetSection("Metadata").GetChildren())
        {
            metadata[entry.Key] = entry.Value ?? string.Empty;
        }

        return Task.FromResult<ISystemUnderTest>(new DeployedSut(endpoints, readiness, metadata, ReadyTimeout()));
    }

    private static Uri ParseEndpoint(IConfigurationSection endpoint) =>
        Uri.TryCreate(endpoint.Value, UriKind.Absolute, out var url) && (url.Scheme == Uri.UriSchemeHttp || url.Scheme == Uri.UriSchemeHttps)
            ? url
            : throw new InvalidOperationException($"{endpoint.Path} is '{endpoint.Value}', which is not an absolute http or https URL.");

    private TimeSpan ReadyTimeout()
    {
        var timeout = configuration[ReadyTimeoutKey] is { } configured
            ? TimeSpan.Parse(configured, CultureInfo.InvariantCulture)
            : DefaultReadyTimeout;
        var multiplier = configuration[TimeoutMultiplierKey] is { } scale
            ? double.Parse(scale, CultureInfo.InvariantCulture)
            : 1;
        return timeout * multiplier;
    }
}
