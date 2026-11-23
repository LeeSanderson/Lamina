using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Lamina.Tests;

public sealed class EventuallyTests
{
    private static readonly TimeSpan Tick = TimeSpan.FromMilliseconds(1);

    [Fact]
    public async Task BackOffStartsAt50MsGrowsByHalfAgainTo500MsWithJitterAndProbesOnceMoreAtTheDeadline()
    {
        var clock = new FakeTimeProvider();
        await using var run = await StartAsync(clock);
        await using var scope = run.CreateScope(channel: null, specName: "spec");
        var probes = new List<TimeSpan>();
        var start = clock.GetTimestamp();

        var waiting = Eventually(scope).Until(
            () =>
            {
                probes.Add(clock.GetElapsedTime(start));
                return Task.FromResult(0);
            },
            value => value == 1,
            TimeSpan.FromSeconds(10),
            "the value should become 1");
        await Should.ThrowAsync<EventuallyFailedException>(DriveAsync(clock, waiting));

        var gaps = probes.Zip(probes.Skip(1), (earlier, later) => (later - earlier).TotalMilliseconds).ToList();
        var nominal = Nominal(gaps.Count - 1);
        for (var i = 0; i < gaps.Count - 1; i++)
        {
            gaps[i].ShouldBeInRange((nominal[i] * 0.8) - 1, (nominal[i] * 1.2) + 1, $"gap {i}");
        }

        gaps.Zip(nominal).ShouldContain(pair => Math.Abs(pair.First - pair.Second) > 1, "the delays should be jittered");
        probes[0].ShouldBe(TimeSpan.Zero);
        probes[^1].ShouldBe(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task ThePredicateShapeReturnsTheFirstValueThatMeetsTheCondition()
    {
        var clock = new FakeTimeProvider();
        await using var run = await StartAsync(clock);
        await using var scope = run.CreateScope(channel: null, specName: "spec");
        var probes = 0;

        var waiting = Eventually(scope).Until(() => Task.FromResult(++probes), value => value >= 3, TimeSpan.FromSeconds(5), "three probes");

        (await DriveAsync(clock, waiting)).ShouldBe(3);
    }

    [Fact]
    public async Task TheAssertionShapeReturnsTheFirstValueThatPassesTheAssertion()
    {
        var clock = new FakeTimeProvider();
        await using var run = await StartAsync(clock);
        await using var scope = run.CreateScope(channel: null, specName: "spec");
        var probes = 0;

        var waiting = Eventually(scope).Until(() => Task.FromResult(++probes), value => value.ShouldBe(3), TimeSpan.FromSeconds(5), "three probes");

        (await DriveAsync(clock, waiting)).ShouldBe(3);
    }

    [Fact]
    public async Task AProbeThatThrowsIsRecordedAsAnObservationAndPollingGoesOn()
    {
        var clock = new FakeTimeProvider();
        await using var run = await StartAsync(clock);
        await using var scope = run.CreateScope(channel: null, specName: "spec");
        var probes = 0;

        var waiting = Eventually(scope).Until(
            () => ++probes < 3 ? throw new InvalidOperationException("Service\nunavailable") : Task.FromResult("Pending"),
            value => value == "Confirmed",
            TimeSpan.FromSeconds(1),
            "the order should be confirmed");
        var failure = await Should.ThrowAsync<EventuallyFailedException>(DriveAsync(clock, waiting));

        var lines = Lines(failure);
        lines[1].ShouldMatch(@"^Last observed \(attempt \d+, 1\.0s\): ""Pending""$");
        lines[2].ShouldBe("Observations, oldest first:");
        lines[3].ShouldMatch(@"^  0\.0s-0\.\d+s  +x2  probe threw InvalidOperationException: Service unavailable$");
        lines[4].ShouldMatch(@"^  0\.\d+s-1\.0s  +x\d+  ""Pending""$");
        lines.Length.ShouldBe(5);
    }

    [Fact]
    public async Task OperationCanceledExceptionFromAProbeIsNotCaught()
    {
        var clock = new FakeTimeProvider();
        await using var run = await StartAsync(clock);
        await using var scope = run.CreateScope(channel: null, specName: "spec");

        var waiting = Eventually(scope).Until<int>(
            () => throw new OperationCanceledException(),
            value => value == 1,
            TimeSpan.FromSeconds(1),
            "never");

        await Should.ThrowAsync<OperationCanceledException>(waiting);
    }

    [Fact]
    public async Task TheMessageNamesTheReasonTheTimeAllowedAndTheLastObservationAsOneLineJson()
    {
        var clock = new FakeTimeProvider();
        await using var run = await StartAsync(clock);
        await using var scope = run.CreateScope(channel: null, specName: "spec");
        var order = new Order(7, "Pending", Reason: null);

        var waiting = Eventually(scope).Until(() => Task.FromResult(order), o => o.Status == "Confirmed", TimeSpan.FromSeconds(5), "the order should be confirmed");
        var failure = await Should.ThrowAsync<EventuallyFailedException>(DriveAsync(clock, waiting));

        var lines = Lines(failure);
        lines[0].ShouldBe("Gave up waiting: the order should be confirmed (within 5.0s).");
        lines[1].ShouldMatch(@"^Last observed \(attempt \d+, 5\.0s\): \{""Id"":7,""Status"":""Pending"",""Reason"":null\}$");
        lines.Length.ShouldBe(2, "one distinct observation has no history");
    }

    [Fact]
    public async Task TheTimeoutMultiplierScalesTheWaitAndTheMessageShowsBothFigures()
    {
        var clock = new FakeTimeProvider();
        await using var run = await TestSuite.StartAsync(lamina =>
        {
            lamina.Configuration["Lamina:TimeoutMultiplier"] = "2.5";
            lamina.Services.AddSingleton<TimeProvider>(clock);
        });
        await using var scope = run.CreateScope(channel: null, specName: "spec");
        var start = clock.GetTimestamp();
        var lastProbe = TimeSpan.Zero;

        var waiting = Eventually(scope).Until(
            () =>
            {
                lastProbe = clock.GetElapsedTime(start);
                return Task.FromResult(0);
            },
            value => value == 1,
            TimeSpan.FromSeconds(1),
            "the value should become 1");
        var failure = await Should.ThrowAsync<EventuallyFailedException>(DriveAsync(clock, waiting));

        lastProbe.ShouldBe(TimeSpan.FromSeconds(2.5));
        Lines(failure)[0].ShouldBe("Gave up waiting: the value should become 1 (within 1.0s x 2.5 = 2.5s).");
    }

    [Fact]
    public async Task TheAssertionsMessageIsShownUnderTheLastObservation()
    {
        var clock = new FakeTimeProvider();
        await using var run = await StartAsync(clock);
        await using var scope = run.CreateScope(channel: null, specName: "spec");

        var waiting = Eventually(scope).Until(
            () => Task.FromResult<IReadOnlyList<int>>([1]),
            creates => creates.Count.ShouldBe(2, "expected a retry"),
            TimeSpan.FromSeconds(1),
            "the payment should be retried");
        var failure = await Should.ThrowAsync<EventuallyFailedException>(DriveAsync(clock, waiting));

        var lines = Lines(failure);
        lines[1].ShouldEndWith(": [1]");
        lines[2].ShouldStartWith("  ");
        lines[2].ShouldContain("expected a retry");
        lines.Length.ShouldBe(3);
    }

    [Fact]
    public async Task AliasesInTheMessageAreAnnotatedWithTheirNames()
    {
        var clock = new FakeTimeProvider();
        await using var run = await StartAsync(clock);
        await using var scope = run.CreateScope(channel: null, specName: "spec");
        var dune = scope.Aliases.For("Dune");

        var waiting = Eventually(scope).Until(() => Task.FromResult(new { Title = dune }), book => book.Title == "Emma", TimeSpan.FromSeconds(1), "the title should change");
        var failure = await Should.ThrowAsync<EventuallyFailedException>(DriveAsync(clock, waiting));

        Lines(failure)[1].ShouldEndWith($": {{\"Title\":\"Dune ({dune})\"}}");
    }

    [Fact]
    public async Task ObservedValuesAreTruncatedAt1000Characters()
    {
        var clock = new FakeTimeProvider();
        await using var run = await StartAsync(clock);
        await using var scope = run.CreateScope(channel: null, specName: "spec");
        var huge = new string('x', 2000);

        var waiting = Eventually(scope).Until(() => Task.FromResult(huge), value => value.Length == 0, TimeSpan.FromSeconds(1), "the value should empty");
        var failure = await Should.ThrowAsync<EventuallyFailedException>(DriveAsync(clock, waiting));

        var observed = Lines(failure)[1].Split("): ", 2)[1];
        observed.ShouldBe("\"" + new string('x', 999) + "…");
    }

    private sealed record Order(int Id, string Status, string? Reason);

    private static string[] Lines(Exception failure) => failure.Message.Split(Environment.NewLine);

    private static List<double> Nominal(int count)
    {
        var delays = new List<double>();
        var delay = 50.0;
        for (var i = 0; i < count; i++)
        {
            delays.Add(delay);
            delay = Math.Min(delay * 1.5, 500);
        }

        return delays;
    }

    private static Eventually Eventually(SpecScope scope) => scope.Services.GetRequiredService<Eventually>();

    private static Task<LaminaRun> StartAsync(FakeTimeProvider clock) =>
        TestSuite.StartAsync(lamina => lamina.Services.AddSingleton<TimeProvider>(clock));

    private static async Task<T> DriveAsync<T>(FakeTimeProvider clock, Task<T> waiting)
    {
        while (!waiting.IsCompleted)
        {
            clock.Advance(Tick);
        }

        return await waiting;
    }
}
