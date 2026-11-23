namespace Lamina.Sut.Deployed;

internal sealed class ReadinessProbe(string name, Uri url)
{
    internal string Name => name;

    internal Uri Url => url;

    internal bool IsReady { get; private set; }

    internal string LastOutcome { get; private set; } = "had not answered";

    internal async Task PollAsync(HttpClient http, TimeSpan interval, CancellationToken ct)
    {
        while (!await AnswersSuccessAsync(http, ct).ConfigureAwait(false))
        {
            await Task.Delay(interval, ct).ConfigureAwait(false);
        }

        IsReady = true;
    }

    private async Task<bool> AnswersSuccessAsync(HttpClient http, CancellationToken ct)
    {
        try
        {
            using var response = await http.GetAsync(url, ct).ConfigureAwait(false);
            LastOutcome = $"answered {response.StatusCode:D} ({response.ReasonPhrase})";
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException error)
        {
            LastOutcome = $"failed: {error.Message}";
            return false;
        }
        catch (TaskCanceledException error) when (!ct.IsCancellationRequested)
        {
            LastOutcome = $"failed: {error.Message}";
            return false;
        }
    }
}
