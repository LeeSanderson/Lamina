namespace Lamina.XunitV3.Tests.Samples;

public sealed class ConstructorMisuseSpecs : SampleSpec
{
    private readonly string _shelved;

    public ConstructorMisuseSpecs() => _shelved = Library.Shelve("Dune");

    [Fact]
    public void UsingADslFromTheConstructorFails() => Assert.NotNull(_shelved);
}
