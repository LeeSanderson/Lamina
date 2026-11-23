using Lamina.Dsl;

namespace Lamina.XunitV3.Tests.Samples;

public sealed class LibraryDsl(ISystemUnderTest sut) : LaminaDsl
{
    public string Shelve(string title) => Alias(title);

    public Uri Catalogue() => sut.Endpoint("catalogue");

    public Task<string> ConfirmLent(string title)
    {
        var shelved = Alias(title);
        return Eventually.Until(() => Task.FromResult(shelved), lent => lent.Length == 0, TimeSpan.FromMilliseconds(200), $"{title} should be lent");
    }
}
