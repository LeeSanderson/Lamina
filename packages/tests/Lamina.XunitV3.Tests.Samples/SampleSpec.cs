namespace Lamina.XunitV3.Tests.Samples;

public abstract class SampleSpec : LaminaSpec<SampleSuite>
{
    protected LibraryDsl Library => Dsl<LibraryDsl>();

    protected ShelfDsl Shelf => Dsl<ShelfDsl>();
}
