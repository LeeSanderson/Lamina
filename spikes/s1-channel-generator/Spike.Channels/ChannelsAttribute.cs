namespace Spike.Channels;

/// <summary>
/// Expands an abstract partial spec class into one sealed subclass per channel.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ChannelsAttribute(params string[] channels) : Attribute
{
    public IReadOnlyList<string> Channels { get; } = channels;
}
