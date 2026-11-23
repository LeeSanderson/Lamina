using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Lamina;

/// <summary>
/// Waits for a condition by probing on a back-off, and fails with what it observed. One per spec: DSLs reach it through
/// <c>LaminaDsl.Eventually</c>, and a driver or stub that must wait injects it.
/// </summary>
/// <remarks>
/// Probes start 50 ms apart and back off by half again each time, to at most 500 ms, with ±20 % jitter. There is always one
/// last probe at the deadline. Every <c>within</c> is scaled by <c>Lamina:TimeoutMultiplier</c>, and time is the run's wall clock,
/// never the system under test's: the run's <see cref="TimeProvider"/> service, <see cref="TimeProvider.System"/> unless a suite registers another.
/// </remarks>
[SuppressMessage("Minor Code Smell", "S4261:Methods should be named according to their synchronicities", Justification = "Spec-mandated name: specs read Eventually.Until(...).")]
[SuppressMessage("Major Code Smell", "S2221:Exception should not be caught", Justification = "Whatever a probe or assertion throws is an observation; only cancellation propagates.")]
public sealed class Eventually
{
    private const int MaxRenderedLength = 1000;
    private const double BackOffFactor = 1.5;
    private const double Jitter = 0.2;
    private static readonly TimeSpan FirstDelay = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan MaxDelay = TimeSpan.FromMilliseconds(500);
    private static readonly JsonSerializerOptions Json = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, RespectNullableAnnotations = true, RespectRequiredConstructorParameters = true };

    private readonly SpecScope _scope;
    private readonly TimeProvider _clock;
    private readonly double _multiplier;

    internal Eventually(SpecScope scope, TimeProvider clock, double multiplier)
    {
        _scope = scope;
        _clock = clock;
        _multiplier = multiplier;
    }

    /// <summary>
    /// Probes until <paramref name="condition"/> holds for the probed value, and returns that value. A probe that throws is
    /// recorded as an observation. Throws <see cref="EventuallyFailedException"/> naming <paramref name="because"/> and what
    /// was observed if <paramref name="within"/> runs out.
    /// </summary>
    public Task<T> Until<T>(Func<Task<T>> probe, Func<T, bool> condition, TimeSpan within, string because)
    {
        ArgumentNullException.ThrowIfNull(probe);
        ArgumentNullException.ThrowIfNull(condition);
        return PollAsync(probe, value => condition(value) ? Verdict.Met : Verdict.NotMet(why: null), within, because);
    }

    /// <summary>
    /// Probes until <paramref name="assertion"/> passes for the probed value, and returns that value. The assertion's message
    /// is shown under each value it rejected. Throws <see cref="EventuallyFailedException"/> if <paramref name="within"/> runs out.
    /// </summary>
    public Task<T> Until<T>(Func<Task<T>> probe, Action<T> assertion, TimeSpan within, string because)
    {
        ArgumentNullException.ThrowIfNull(probe);
        ArgumentNullException.ThrowIfNull(assertion);
        return PollAsync(probe, value => Check(assertion, value), within, because);
    }

    private static Verdict Check<T>(Action<T> assertion, T value)
    {
        try
        {
            assertion(value);
            return Verdict.Met;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Verdict.NotMet(OneLine(exception.Message));
        }
    }

    private async Task<T> PollAsync<T>(Func<Task<T>> probe, Func<T, Verdict> check, TimeSpan within, string because)
    {
        var allowed = within * _multiplier;
        var started = _clock.GetTimestamp();
        var history = new List<Observation>();
        var delay = FirstDelay;
        var attempt = 0;

        while (true)
        {
            attempt++;
            var at = _clock.GetElapsedTime(started);
            string seen;
            string? why;
            try
            {
                var last = await probe().ConfigureAwait(false);
                var verdict = check(last);
                if (verdict.IsMet)
                {
                    return last;
                }

                seen = Render(last);
                why = verdict.Why;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                seen = $"probe threw {exception.GetType().Name}: {OneLine(exception.Message)}";
                why = null;
            }

            Record(history, at, seen, why);

            var left = allowed - _clock.GetElapsedTime(started);
            if (left <= TimeSpan.Zero)
            {
                throw new EventuallyFailedException(Describe(because, within, allowed, attempt, history));
            }

            var jittered = delay * (1 - Jitter + (Random.Shared.NextDouble() * 2 * Jitter));
            await Task.Delay(jittered < left ? jittered : left, _clock).ConfigureAwait(false);
            delay = delay * BackOffFactor < MaxDelay ? delay * BackOffFactor : MaxDelay;
        }
    }

    private string Describe(string because, TimeSpan within, TimeSpan allowed, int attempts, List<Observation> history)
    {
        var limit = allowed == within
            ? Seconds(within)
            : string.Create(CultureInfo.InvariantCulture, $"{Seconds(within)} x {_multiplier:0.##} = {Seconds(allowed)}");
        var last = history[^1];
        var message = new StringBuilder()
            .Append(CultureInfo.InvariantCulture, $"Gave up waiting: {because} (within {limit}).").AppendLine()
            .Append(CultureInfo.InvariantCulture, $"Last observed (attempt {attempts}, {Seconds(last.Last)}): {Annotated(last.Seen)}");
        if (last.Why is not null)
        {
            message.AppendLine().Append("  ").Append(Annotated(last.Why));
        }

        if (history.Count > 1)
        {
            message.AppendLine().Append("Observations, oldest first:");
            var spans = history.Select(o => $"{Seconds(o.First)}-{Seconds(o.Last)}").ToList();
            var width = spans.Max(s => s.Length);
            foreach (var (observation, span) in history.Zip(spans))
            {
                message.AppendLine()
                    .Append(CultureInfo.InvariantCulture, $"  {span.PadRight(width)}  x{observation.Count}  {Annotated(observation.Seen)}");
                if (observation.Why is not null)
                {
                    message.Append(CultureInfo.InvariantCulture, $"  ({Annotated(observation.Why)})");
                }
            }
        }

        return message.ToString();
    }

    private string Annotated(string text) => Truncated(_scope.Aliases.Annotate(text));

    private static void Record(List<Observation> history, TimeSpan at, string seen, string? why)
    {
        if (history.Count > 0 && history[^1].IsSameAs(seen, why))
        {
            history[^1] = history[^1] with { Last = at, Count = history[^1].Count + 1 };
        }
        else
        {
            history.Add(new Observation(at, at, 1, seen, why));
        }
    }

    private static string Render<T>(T value)
    {
        if (value is null)
        {
            return "null";
        }

        try
        {
            return JsonSerializer.Serialize(value, Json);
        }
        catch (NotSupportedException)
        {
            return value.ToString() ?? value.GetType().Name;
        }
    }

    private static string Truncated(string text) => text.Length <= MaxRenderedLength ? text : string.Concat(text.AsSpan(0, MaxRenderedLength), "…");

    private static string OneLine(string text) =>
        string.Join(' ', text.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));

    private static string Seconds(TimeSpan time) => time.TotalSeconds.ToString("0.0#", CultureInfo.InvariantCulture) + "s";

    private sealed record Observation(TimeSpan First, TimeSpan Last, int Count, string Seen, string? Why)
    {
        public bool IsSameAs(string seen, string? why) => Seen == seen && Why == why;
    }

    private readonly record struct Verdict(bool IsMet, string? Why)
    {
        public static Verdict Met => new(IsMet: true, Why: null);

        public static Verdict NotMet(string? why) => new(IsMet: false, why);
    }
}
