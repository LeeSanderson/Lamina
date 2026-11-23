namespace Lamina.Stubs;

internal sealed class StubContext(ISystemUnderTest sut) : IDisposable
{
    internal ISystemUnderTest Sut => sut;

    internal HttpClient Http { get; } = new();

    public void Dispose() => Http.Dispose();
}
