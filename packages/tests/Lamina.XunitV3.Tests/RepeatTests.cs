using System.Text.RegularExpressions;

namespace Lamina.XunitV3.Tests;

public sealed partial class RepeatTests(SamplesRun samples, RepeatedApiChannelRun repeated) : IClassFixture<SamplesRun>, IClassFixture<RepeatedApiChannelRun>
{
    private const string Spec = "EachCopyWritesItsXunitTestCaseId";

    [Fact]
    public void WithRepeatUnsetEachSpecRunsOnceUnderItsNormalName()
    {
        var result = samples.Single("RepeatedSpecs_Api", Spec);

        result.Passed.ShouldBeTrue(result.FailureMessage);
        result.DisplayName.ShouldBe($"Lamina.XunitV3.Tests.Samples.RepeatedSpecs_Api.{Spec}");
    }

    [Fact]
    public void WithRepeatThreeEachSpecRunsAsThreeNamedCopies()
    {
        var copies = repeated.All("RepeatedSpecs_Api");

        copies.ShouldAllBe(c => c.Passed);
        copies.Select(c => c.DisplayName).Order(StringComparer.Ordinal).ShouldBe(
        [
            $"Lamina.XunitV3.Tests.Samples.RepeatedSpecs_Api.{Spec} [copy 1/3]",
            $"Lamina.XunitV3.Tests.Samples.RepeatedSpecs_Api.{Spec} [copy 2/3]",
            $"Lamina.XunitV3.Tests.Samples.RepeatedSpecs_Api.{Spec} [copy 3/3]",
        ]);
    }

    [Fact]
    public void EachCopyHasItsOwnXunitTestCaseAndLaminaTestId()
    {
        var copies = repeated.All("RepeatedSpecs_Api");

        copies.Select(c => XunitId.Match(c.Output).Groups["id"].Value).Distinct(StringComparer.Ordinal).Count().ShouldBe(3);
        copies.Select(c => LaminaId.Match(c.Output).Groups["id"].Value).Distinct(StringComparer.Ordinal).Count().ShouldBe(3);
    }

    [Fact]
    public void CopiesKeepTheirChannelTraitSoAChannelFilterStillSelectsThem()
    {
        repeated.All("RepeatedSpecs_Api").ShouldAllBe(c => c.Traits["Channel"].Single() == "api");
        repeated.All("ChannelledSpecs_Api").Count.ShouldBe(3);
        repeated.Results.ShouldAllBe(r => r.TypeName.EndsWith("_Api", StringComparison.Ordinal));
    }

    [GeneratedRegex(@"xUnit test case (?<id>\S+)", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex XunitId { get; }

    [GeneratedRegex(@"Test (?<id>[a-z0-9]{6}) on", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex LaminaId { get; }
}
