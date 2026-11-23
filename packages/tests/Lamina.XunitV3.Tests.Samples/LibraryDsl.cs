using Lamina.Dsl;

namespace Lamina.XunitV3.Tests.Samples;

public sealed class LibraryDsl(ISystemUnderTest sut) : LaminaDsl
{
    public string Shelve(string title) => Alias(title);

    public Uri Catalogue() => sut.Endpoint("catalogue");
}
