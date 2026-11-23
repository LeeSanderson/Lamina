namespace Lamina;

internal sealed class SutAccessor
{
    private ISystemUnderTest? _sut;

    internal ISystemUnderTest Sut
    {
        get => _sut ?? throw new InvalidOperationException("The system under test isn't started yet.");
        set => _sut = value;
    }
}
