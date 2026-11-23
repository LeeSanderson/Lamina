using System.Collections.Concurrent;
using System.Reflection;
using Xunit.SimpleRunner;

namespace Lamina.XunitV3.Tests;

/// <summary>
/// Runs Lamina.XunitV3.Tests.Samples once, out of process, and keeps every test's result.
/// </summary>
public sealed class SamplesRun : IAsyncLifetime
{
    private readonly ConcurrentBag<SampleResult> _results = [];

    private readonly ConcurrentQueue<string> _diagnostics = [];

    public IReadOnlyCollection<SampleResult> Results => _results;

    public IReadOnlyCollection<string> Diagnostics => _diagnostics;

    public SampleResult Single(string typeName, string methodName) =>
        _results.Single(r => r.TypeName.EndsWith($".{typeName}", StringComparison.Ordinal) && r.MethodName == methodName);

    public IReadOnlyList<SampleResult> All(string typeName) =>
        [.. _results.Where(r => r.TypeName.EndsWith($".{typeName}", StringComparison.Ordinal))];

    public async ValueTask InitializeAsync()
    {
        var samples = typeof(SamplesRun).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(a => a.Key == "SamplesAssembly").Value!;
        var options = new AssemblyRunnerOptions(Path.GetFullPath(samples))
        {
            OnTestPassed = test => _results.Add(new(test.TypeName ?? "", test.MethodName ?? "", Passed: true, test.Output, FailureMessage: null)),
            OnTestFailed = test => _results.Add(new(test.TypeName ?? "", test.MethodName ?? "", Passed: false, test.Output, test.Exception.Message)),
            OnDiagnosticMessage = message => _diagnostics.Enqueue(message.Message),
        };
        await using var runner = new AssemblyRunner(options);
        await runner.Run();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
