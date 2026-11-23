using System.Diagnostics.CodeAnalysis;

namespace Lamina;

/// <summary>
/// Names a value a spec remembers. <typeparamref name="T"/> makes recalling it typed.
/// </summary>
[SuppressMessage("Major Code Smell", "S2326:Unused type parameters should be removed", Justification = "T is a phantom type that ties Remember and Recall to one value type.")]
public sealed record Key<T>(string Name)
    where T : notnull;
