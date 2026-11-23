using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace Lamina;

internal sealed class TestIdMinter(Func<string> candidates)
{
    private const int Length = 6;
    private const string Alphabet = "abcdefghijklmnopqrstuvwxyz0123456789";

    private readonly ConcurrentDictionary<string, byte> _used = new(StringComparer.Ordinal);

    internal TestIdMinter()
        : this(() => RandomNumberGenerator.GetString(Alphabet, Length))
    {
    }

    internal string Next()
    {
        while (true)
        {
            var id = candidates();
            if (_used.TryAdd(id, 0))
            {
                return id;
            }
        }
    }
}
