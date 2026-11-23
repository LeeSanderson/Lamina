using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;

namespace Lamina.Dsl;

public static class LaminaBuilderDslExtensions
{
    /// <summary>
    /// Registers every concrete <see cref="LaminaDsl"/> in <typeparamref name="T"/>'s assembly as scoped: one instance per spec.
    /// </summary>
    [SuppressMessage("Critical Code Smell", "S4018:Generic methods should provide type parameters", Justification = "T only names an assembly, as in AddValidatorsFromAssemblyOf<T>; the caller always states it.")]
    public static LaminaBuilder AddDslsFromAssemblyOf<T>(this LaminaBuilder builder)
    {
        var dslTypes = typeof(T).Assembly.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false } && t.IsSubclassOf(typeof(LaminaDsl)));
        foreach (var dslType in dslTypes)
        {
            builder.Services.AddScoped(dslType, services => Create(services, dslType));
        }

        return builder;
    }

    private static LaminaDsl Create(IServiceProvider services, Type dslType)
    {
        var dsl = ActivatorUtilities.CreateInstance(services, dslType) as LaminaDsl
            ?? throw new InvalidOperationException($"{dslType.Name} is not a LaminaDsl.");
        dsl.Scope = services.GetRequiredService<SpecScope>();
        return dsl;
    }
}
