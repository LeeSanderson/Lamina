// PROTOTYPE stand-in for `Lamina.Dsl`.
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Lamina.Dsl;

/// <summary>Pins every capability this DSL's constructor asks for to the default channel.</summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class ArrangementDslAttribute : Attribute;

/// <summary>
/// Base for team DSLs. Drivers and stubs arrive by constructor injection (Plan.md §6.3 shape);
/// the spec scope is attached by Lamina after construction, so team DSLs don't have to pass it to a base constructor.
/// </summary>
public abstract class LaminaDsl
{
    private SpecScope? scope;

    protected SpecScope Scope => scope ?? throw new InvalidOperationException("DSL used outside a spec scope.");

    protected string Alias(string name) => Scope.Aliases.For(name);

    protected void Remember<T>(Key<T> key, T value) where T : notnull => Scope.Remember(key, value);

    protected T Recall<T>(Key<T> key) where T : notnull => Scope.Recall(key);

    protected T Recall<T>(Key<T> key, T orDefault) where T : notnull => Scope.TryRecall(key, out var value) ? value : orDefault;

    internal static LaminaDsl Create(IServiceProvider sp, Type dslType)
    {
        var dsl = dslType.GetCustomAttribute<ArrangementDslAttribute>() is null
            ? (LaminaDsl)ActivatorUtilities.CreateInstance(sp, dslType)
            : (LaminaDsl)ActivatorUtilities.CreateInstance(sp, dslType, PinnedCapabilities(sp, dslType));
        dsl.scope = sp.GetRequiredService<SpecScope>();
        return dsl;
    }

    // Arrangement pinning: resolve each capability parameter on the default channel instead of the spec's,
    // and hand them to the activator as explicit arguments.
    private static object[] PinnedCapabilities(IServiceProvider sp, Type dslType)
    {
        var builder = sp.GetRequiredService<LaminaBuilder>();
        var defaultChannel = sp.GetRequiredService<ChannelModel>().Default;
        return dslType.GetConstructors().Single().GetParameters()
            .Where(p => builder.Capabilities.Contains(p.ParameterType))
            .Select(p => LaminaBuilder.ResolveCapability(sp, p.ParameterType, defaultChannel))
            .ToArray();
    }
}
