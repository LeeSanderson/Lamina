namespace Lamina.Dsl;

/// <summary>
/// Base for a team's DSLs. Take capabilities by constructor; Lamina attaches the spec's scope after construction.
/// </summary>
public abstract class LaminaDsl
{
    private SpecScope? _scope;

    internal SpecScope Scope
    {
        get => _scope ?? throw new InvalidOperationException(
            $"{GetType().Name} has no spec scope. Resolve DSLs from a spec's services, and register them with AddDslsFromAssemblyOf<T>().");
        set => _scope = value;
    }

    /// <summary>
    /// Returns the spec's alias for <paramref name="name"/>: the same value every time within the spec.
    /// </summary>
    protected string Alias(string name) => Scope.Aliases.For(name);

    protected void Remember<T>(Key<T> key, T value)
        where T : notnull =>
        Scope.Remember(key, value);

    protected T Recall<T>(Key<T> key)
        where T : notnull =>
        Scope.Recall(key);

    protected T Recall<T>(Key<T> key, T orDefault)
        where T : notnull =>
        Scope.Recall(key, orDefault);
}
