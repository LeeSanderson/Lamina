namespace Lamina.XunitV3.Tests;

public sealed class RunDiagnosticsTests(SamplesRun samples) : IClassFixture<SamplesRun>
{
    [Fact]
    public void EachRunWritesOneStartLineAsADiagnosticMessage()
    {
        samples.Diagnostics.Where(m => m.StartsWith("Lamina run started", StringComparison.Ordinal)).ShouldHaveSingleItem()
            .ShouldMatch(@"^Lamina run started with SUT provider 'fake', ready in \d+\.\d s\. Endpoints: none\. Metadata: none\.$");
    }

    [Fact]
    public void EachRunWritesItsTeardownTimeAsADiagnosticMessage()
    {
        samples.Diagnostics.Where(m => m.StartsWith("Lamina run torn down", StringComparison.Ordinal)).ShouldHaveSingleItem()
            .ShouldMatch(@"^Lamina run torn down in \d+\.\d s\.$");
    }
}
