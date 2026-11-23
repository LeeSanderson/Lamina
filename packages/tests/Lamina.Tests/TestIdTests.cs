using System.Text.RegularExpressions;

namespace Lamina.Tests;

public sealed partial class TestIdTests
{
    [Fact]
    public async Task ATestIdIsSixLowercaseAlphanumerics()
    {
        await using var run = await TestSuite.StartAsync();

        for (var i = 0; i < 200; i++)
        {
            await using var scope = run.CreateScope(channel: null, specName: "spec");
            SixLowercaseAlphanumerics.IsMatch(scope.TestId).ShouldBeTrue(scope.TestId);
        }
    }

    [Fact]
    public async Task TestIdsNeverRepeatWithinARun()
    {
        await using var run = await TestSuite.StartAsync();
        var scopes = Enumerable.Range(0, 1000).Select(i => run.CreateScope(channel: null, specName: $"spec {i}")).ToList();

        scopes.Select(s => s.TestId).Distinct(StringComparer.Ordinal).Count().ShouldBe(1000);

        foreach (var scope in scopes)
        {
            await scope.DisposeAsync();
        }
    }

    [Fact]
    public void AForcedCollisionIsSkipped()
    {
        var candidates = new Queue<string>(["aaaaaa", "aaaaaa", "aaaaaa", "bbbbbb"]);
        var minter = new TestIdMinter(candidates.Dequeue);

        minter.Next().ShouldBe("aaaaaa");
        minter.Next().ShouldBe("bbbbbb");
    }

    [GeneratedRegex("^[a-z0-9]{6}$", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex SixLowercaseAlphanumerics { get; }
}
