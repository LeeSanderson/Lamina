using System.Text;

namespace Lamina.Sut.Deployed;

internal sealed class DeployedSut(
    IReadOnlyDictionary<string, Uri> endpoints,
    IReadOnlyDictionary<string, string> readiness,
    IReadOnlyDictionary<string, string> metadata,
    TimeSpan readyTimeout) : ISystemUnderTest
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(5);

    public IReadOnlyDictionary<string, string> Metadata => metadata;

    public Uri Endpoint(string name) =>
        endpoints.TryGetValue(name, out var endpoint)
            ? endpoint
            : throw new ArgumentException(
                $"Lamina:Sut:Deployed:Endpoints has no endpoint named '{name}'. Known: {string.Join(", ", endpoints.Keys.Order(StringComparer.Ordinal))}.",
                nameof(name));

    public async Task WaitUntilReadyAsync(CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(readyTimeout);
        using var http = new HttpClient { Timeout = ProbeTimeout };
        var probes = readiness
            .Select(r => new ReadinessProbe(r.Key, new Uri(Endpoint(r.Key), r.Value)))
            .ToList();
        try
        {
            await Task.WhenAll(probes.Select(p => p.PollAsync(http, PollInterval, timeout.Token))).ConfigureAwait(false);
        }
        catch (OperationCanceledException error) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException(DescribeNotReady(probes), error);
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private string DescribeNotReady(IEnumerable<ReadinessProbe> probes)
    {
        var message = new StringBuilder()
            .Append("The deployed SUT was not ready within ").Append(readyTimeout).Append(". Not ready:");
        foreach (var probe in probes.Where(p => !p.IsReady))
        {
            message.AppendLine().Append("- ").Append(probe.Name).Append(": ").Append(probe.Url).Append(' ').Append(probe.LastOutcome);
        }

        return message.ToString();
    }
}
