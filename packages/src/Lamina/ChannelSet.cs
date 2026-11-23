namespace Lamina;

internal sealed class ChannelSet(IReadOnlyList<string> all, string defaultChannel)
{
    internal string Default => defaultChannel;

    internal string Resolve(string? channel)
    {
        if (channel is null)
        {
            return defaultChannel;
        }

        return all.Contains(channel, StringComparer.Ordinal)
            ? channel
            : throw new InvalidOperationException($"Channel '{channel}' isn't added to the suite. Added: {string.Join(", ", all)}.");
    }
}
