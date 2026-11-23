using System.Reflection;
using Xunit.v3;

namespace Lamina.XunitV3;

/// <summary>
/// xUnit's test framework, plus <c>Lamina:Repeat</c>: when it is N above 1, discovery turns each spec into N copies named
/// <c>&lt;spec name&gt; [copy k/N]</c>. Register it once per suite assembly, next to the run's fixture, with
/// <c>[assembly: TestFramework(typeof(LaminaTestFramework&lt;MySuite&gt;))]</c>. The setting is read from the suite's
/// configuration (its <c>appsettings.json</c> files and environment variables, such as <c>Lamina__Repeat=50</c>).
/// </summary>
public sealed class LaminaTestFramework<TSuite> : XunitTestFramework
    where TSuite : LaminaSuite, new()
{
    private readonly string? _configFileName;

    public LaminaTestFramework()
    {
    }

    public LaminaTestFramework(string? configFileName)
        : base(configFileName) =>
        _configFileName = configFileName;

    protected override ITestFrameworkDiscoverer CreateDiscoverer(Assembly assembly) =>
        new RepeatingDiscoverer(new XunitTestAssembly(assembly, _configFileName), Repeat.Read(new TSuite().ReadConfiguration()));
}
