namespace S7.Consumer;

/// <summary>
/// Uses the generated type so the generated code is compiled and referenced.
/// </summary>
public static class Uses
{
    /// <summary>
    /// Gets the names from the generated type.
    /// </summary>
    /// <returns>The names.</returns>
    public static int Count() => generated_channels_spec.Names().Count;
}
