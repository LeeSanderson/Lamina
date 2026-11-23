using System.Diagnostics.CodeAnalysis;

namespace Lamina.XunitV3.Tests.Samples;

[SuppressMessage("Blocker Code Smell", "S2699:Tests should include assertions", Justification = "These samples fail on purpose, each in a different way, for FailureFooterTests.")]
public sealed class FailingSpecs : SampleSpec
{
    [Fact]
    public void AnAssertionFails() => Assert.Equal("Emma", Library.Shelve("Dune"));

    [Fact]
    public void AnExceptionIsThrown()
    {
        Library.Shelve("Dune");
        throw new InvalidOperationException("The shelf collapsed.");
    }

    [Fact]
    public async Task EventuallyTimesOut() => await Library.ConfirmLent("Dune");
}
