using System.Text.RegularExpressions;

namespace Lamina.XunitV3.Tests;

public sealed partial class FailureFooterTests(SamplesRun samples) : IClassFixture<SamplesRun>
{
    [Theory]
    [InlineData("AnAssertionFails")]
    [InlineData("AnExceptionIsThrown")]
    [InlineData("EventuallyTimesOut")]
    public void AFailingSpecHasItsFooterInItsFailureMessage(string spec)
    {
        var result = samples.Single("FailingSpecs", spec);

        result.Passed.ShouldBeFalse();
        var message = result.FailureMessage.ShouldNotBeNull();
        var footer = Footer.Match(message);
        footer.Success.ShouldBeTrue(message);
        footer.Groups["channel"].Value.ShouldBe("api");
        footer.Groups["aliases"].Value.ShouldStartWith($"Dune -> Dune-{footer.Groups["id"].Value}");
    }

    [Fact]
    public void TheFooterJoinsTheOriginalFailureRatherThanReplacingIt()
    {
        samples.Single("FailingSpecs", "AnExceptionIsThrown").FailureMessage.ShouldNotBeNull().ShouldContain("The shelf collapsed.");
        samples.Single("FailingSpecs", "EventuallyTimesOut").FailureMessage.ShouldNotBeNull().ShouldContain("Gave up waiting: Dune should be lent (within 0.2s).");
    }

    [Fact]
    public void APassingSpecDoesNotFailBecauseOfItsFooter() =>
        samples.Single("AliasSpecs", "TheSameNameGivesTheSameAliasThroughoutTheSpec").Passed.ShouldBeTrue();

    [GeneratedRegex(@"Test (?<id>[a-z0-9]{6}) on '(?<channel>[^']+)' · aliases: (?<aliases>[^\r\n)]+)", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Footer { get; }
}
