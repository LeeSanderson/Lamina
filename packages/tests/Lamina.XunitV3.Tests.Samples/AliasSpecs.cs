namespace Lamina.XunitV3.Tests.Samples;

public sealed class AliasSpecs : SampleSpec
{
    [Fact]
    public void TheSameNameGivesTheSameAliasThroughoutTheSpec()
    {
        var first = Library.Shelve("Dune");

        Assert.Equal(first, Library.Shelve("Dune"));
        Assert.Equal(new Uri("http://catalogue.test"), Library.Catalogue());
    }
}
