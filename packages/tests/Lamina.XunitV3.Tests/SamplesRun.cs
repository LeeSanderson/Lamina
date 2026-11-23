using System.Collections.Concurrent;
using System.Reflection;
using Xunit.Runner.Common;
using Xunit.SimpleRunner;

namespace Lamina.XunitV3.Tests;

/// <summary>
/// Runs Lamina.XunitV3.Tests.Samples once, out of process, and keeps every test's result.
/// </summary>
public class SamplesRun : IAsyncLifetime
{
    private readonly ConcurrentBag<SampleResult> _results = [];
    private readonly Action<XunitFilters> _filter;

    public SamplesRun()
        : this(_ => { })
    {
    }

    protected SamplesRun(Action<XunitFilters> filter) => _filter = filter;

    public IReadOnlyCollection<SampleResult> Results => _results;

    public SampleResult Single(string typeName, string methodName) =>
        _results.Single(r => r.TypeName.EndsWith($".{typeName}", StringComparison.Ordinal) && r.MethodName == methodName);

    public async ValueTask InitializeAsync()
    {
        var samples = typeof(SamplesRun).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(a => a.Key == "SamplesAssembly").Value!;
        var options = new AssemblyRunnerOptions(Path.GetFullPath(samples))
        {
            OnTestPassed = test => _results.Add(new(test.TypeName ?? "", test.MethodName ?? "", Passed: true, test.Output, FailureMessage: null, test.Traits)),
            OnTestFailed = test => _results.Add(new(test.TypeName ?? "", test.MethodName ?? "", Passed: false, test.Output, test.Exception.Message, test.Traits)),
        };
        _filter(options.Filters);
        await using var runner = new AssemblyRunner(options);
        await runner.Run();
    }

    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}
