namespace Lamina.XunitV3.Tests.Samples;

[Collection(nameof(UnstartableSutSuite))]
public sealed class UnstartableSutSpecs : LaminaSpec<UnstartableSutSuite>
{
    [Fact]
    public void FirstSpec() => Assert.NotNull(Scope);

    [Fact]
    public void SecondSpec() => Assert.NotNull(Scope);

    [Fact]
    public void ThirdSpec() => Assert.NotNull(Scope);
}
