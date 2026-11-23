using System.Text;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Lamina.Sut.Aspire;

internal sealed class AspireSut : ISystemUnderTest
{
    private const int LogLinesPerResource = 20;

    private readonly DistributedApplication _app;
    private readonly IDistributedApplicationTestingBuilder _builder;
    private readonly TimeSpan _readyTimeout;
    private readonly IReadOnlyList<string> _resources;

    internal AspireSut(DistributedApplication app, IDistributedApplicationTestingBuilder builder, string[] args, TimeSpan readyTimeout)
    {
        _app = app;
        _builder = builder;
        _readyTimeout = readyTimeout;
        _resources = [.. builder.Resources.OfType<IComputeResource>().Where(r => !IsHidden(app, r)).Select(r => r.Name)];
        Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["provider"] = "aspire",
            ["apphost"] = builder.AppHostAssembly?.GetName().Name ?? string.Empty,
            ["arguments"] = string.Join(' ', args),
            ["resources"] = string.Join(", ", _resources),
        };
    }

    public IReadOnlyDictionary<string, string> Metadata { get; }

    public Uri Endpoint(string name)
    {
        if (!_resources.Contains(name, StringComparer.Ordinal))
        {
            throw new ArgumentException($"The AppHost has no resource named '{name}'. Resources: {string.Join(", ", _resources)}.", nameof(name));
        }

        return _app.GetEndpoint(name, "http");
    }

    public async Task WaitUntilReadyAsync(CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(_readyTimeout);
        var notifications = _app.ResourceNotifications;
        try
        {
            await Task.WhenAll(_resources.Select(name =>
                notifications.WaitForResourceHealthyAsync(name, WaitBehavior.StopOnResourceUnavailable, timeout.Token))).ConfigureAwait(false);
        }
        catch (Exception error) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException(await DescribeUnhealthyResourcesAsync().ConfigureAwait(false), error);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _app.DisposeAsync().ConfigureAwait(false);
        await _builder.DisposeAsync().ConfigureAwait(false);
    }

    // Aspire adds hidden helper resources, such as a project's "<name>-rebuilder", that never start.
    private static bool IsHidden(DistributedApplication app, IResource resource) =>
        app.ResourceNotifications.TryGetCurrentState(resource.Name, out var current) && current.Snapshot.IsHidden;

    private async Task<string> DescribeUnhealthyResourcesAsync()
    {
        var message = new StringBuilder()
            .Append("The Aspire AppHost was not ready within ").Append(_readyTimeout).Append(". Resources not healthy:");
        foreach (var name in _resources)
        {
            var known = _app.ResourceNotifications.TryGetCurrentState(name, out var current);
            if (!known || current!.Snapshot.HealthStatus != HealthStatus.Healthy)
            {
                await DescribeAsync(message, name, known ? current : null).ConfigureAwait(false);
            }
        }

        return message.ToString();
    }

    private async Task DescribeAsync(StringBuilder message, string name, ResourceEvent? current)
    {
        if (current is null)
        {
            message.AppendLine().Append("- ").Append(name).Append(": not started");
            return;
        }

        message.AppendLine()
            .Append("- ").Append(name)
            .Append(": state ").Append(current.Snapshot.State?.Text ?? "unknown")
            .Append(", health ").Append(current.Snapshot.HealthStatus?.ToString() ?? "unknown");
        foreach (var line in await LastLogLinesAsync(current.ResourceId).ConfigureAwait(false))
        {
            message.AppendLine().Append("    ").Append(line);
        }
    }

    // Logs are keyed by the running instance's ID, such as "late-abcdefgh", not by the resource name.
    private async Task<IEnumerable<string>> LastLogLinesAsync(string resourceId)
    {
        var lines = new List<string>();
        var logs = _app.Services.GetRequiredService<ResourceLoggerService>();
        await foreach (var batch in logs.GetAllAsync(resourceId).ConfigureAwait(false))
        {
            lines.AddRange(batch.Select(l => l.Content));
        }

        return lines.TakeLast(LogLinesPerResource);
    }
}
