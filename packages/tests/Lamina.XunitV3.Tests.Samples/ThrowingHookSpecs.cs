namespace Lamina.XunitV3.Tests.Samples;

[Collection(nameof(ThrowingHookSuite))]
public sealed class ThrowingHookSpecs : LaminaSpec<ThrowingHookSuite>
{
    [Fact]
    public void FirstSpec() => Assert.NotNull(Scope);

    [Fact]
    public void SecondSpec() => Assert.NotNull(Scope);
}
