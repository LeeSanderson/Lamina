using System.Text.RegularExpressions;

namespace Lamina.XunitV3.Tests;

public sealed partial class SpecBaseTests(SamplesRun samples) : IClassFixture<SamplesRun>
{
    [Fact]
    public void SpecsRunWithAFakeSutProviderAndPass()
    {
        samples.Single("AliasSpecs", "TheSameNameGivesTheSameAliasThroughoutTheSpec").Passed.ShouldBeTrue();
        samples.Single("DefaultChannelSpecs", "ASpecWithNoChannelTraitRunsOnTheDefaultChannel").Passed.ShouldBeTrue();
        samples.Single("WebChannelSpecs", "ASpecWithAChannelTraitRunsOnThatChannel").Passed.ShouldBeTrue();
    }

    [Fact]
    public void EverySpecWritesAFooterWithItsTestIdChannelAndAliases()
    {
        var output = samples.Single("AliasSpecs", "TheSameNameGivesTheSameAliasThroughoutTheSpec").Output;

        var footer = Footer.Match(output);
        footer.Success.ShouldBeTrue(output);
        footer.Groups["channel"].Value.ShouldBe("api");
        footer.Groups["aliases"].Value.ShouldBe($"Dune -> Dune-{footer.Groups["id"].Value}");
    }

    [Fact]
    public void ASpecThatUsesNoAliasesSaysSoInItsFooter()
    {
        var output = samples.Single("DefaultChannelSpecs", "ASpecWithNoChannelTraitRunsOnTheDefaultChannel").Output;

        Footer.Match(output).Groups["aliases"].Value.ShouldBe("none");
    }

    [Fact]
    public void TheFooterNamesTheChannelFromTheChannelTrait()
    {
        var output = samples.Single("WebChannelSpecs", "ASpecWithAChannelTraitRunsOnThatChannel").Output;

        Footer.Match(output).Groups["channel"].Value.ShouldBe("web");
    }

    [Fact]
    public void UsingADslFromASpecsConstructorFailsWithAnExplanation()
    {
        var result = samples.Single("ConstructorMisuseSpecs", "UsingADslFromTheConstructorFails");

        result.Passed.ShouldBeFalse();
        result.FailureMessage.ShouldNotBeNull().ShouldContain("created in InitializeAsync");
    }

    [GeneratedRegex(@"^Test (?<id>[a-z0-9]{6}) on '(?<channel>[^']+)' · aliases: (?<aliases>.+?)\r?$", RegexOptions.Multiline, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Footer { get; }
}
