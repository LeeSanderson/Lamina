namespace Lamina;

internal sealed class SpecScopeHolder
{
    private SpecScope? _scope;

    internal SpecScope Scope
    {
        get => _scope ?? throw new InvalidOperationException("This service scope has no spec scope. Create one with LaminaRun.CreateScope.");
        set => _scope = value;
    }
}
