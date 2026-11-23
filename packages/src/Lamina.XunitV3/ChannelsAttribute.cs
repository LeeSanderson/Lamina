namespace Lamina.XunitV3;

/// <summary>
/// Runs an abstract spec class once per channel. The channel generator emits a sealed <c>&lt;Class&gt;_&lt;Channel&gt;</c>
/// subclass for each channel, carrying <c>[Trait("Channel", "&lt;channel&gt;")]</c>, so <c>--filter "Channel=api"</c> selects it.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ChannelsAttribute(params string[] channels) : Attribute
{
    public IReadOnlyList<string> Channels { get; } = channels;
}
