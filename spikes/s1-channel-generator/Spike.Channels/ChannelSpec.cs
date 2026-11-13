namespace Spike.Channels;

/// <summary>
/// Base for specs that run once per channel. The generator overrides <see cref="Channel"/>.
/// </summary>
public abstract class ChannelSpec
{
    protected abstract string Channel { get; }

    public override string ToString() => Channel;
}
