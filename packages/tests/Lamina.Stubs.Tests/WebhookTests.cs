using System.Text.Json;

namespace Lamina.Stubs.Tests;

public sealed class WebhookTests
{
    [Fact]
    public async Task AWebhookPostsItsBodyAsJsonToThePathOnTheNamedEndpoint()
    {
        var (sut, run) = await WebhookSuite.StartAsync();
        await using var _ = run;
        await using var scope = run.CreateScope(channel: null, specName: "spec");
        var stub = scope.Services.GetRequiredService<RecordingStub>();

        await stub.Notify("payments", "webhooks/payment-provider", new { reference = "folio-1", status = "succeeded" });

        var received = sut.Received.ShouldHaveSingleItem();
        received.Path.ShouldBe("/payments/webhooks/payment-provider");
        received.ContentType.ShouldNotBeNull().ShouldStartWith("application/json");
        using var body = JsonDocument.Parse(received.Body);
        body.RootElement.GetProperty("reference").GetString().ShouldBe("folio-1");
        body.RootElement.GetProperty("status").GetString().ShouldBe("succeeded");
    }

    [Fact]
    public async Task AWebhookCarriesTheSpecsTestIdAsBaggage()
    {
        var (sut, run) = await WebhookSuite.StartAsync();
        await using var _ = run;
        await using var first = run.CreateScope(channel: null, specName: "first");
        await using var second = run.CreateScope(channel: null, specName: "second");

        await first.Services.GetRequiredService<RecordingStub>().Notify("payments", "hook", new { n = 1 });
        await second.Services.GetRequiredService<RecordingStub>().Notify("payments", "hook", new { n = 2 });

        sut.Received.Select(r => Baggage(r.Baggage)).ShouldBe(
        [
            [new("lamina.test", first.TestId)],
            [new("lamina.test", second.TestId)],
        ]);
    }

    [Fact]
    public async Task AWebhookTheSutRefusesThrowsWithItsStatusAndAnswer()
    {
        var (_, run) = await WebhookSuite.StartAsync();
        await using var _ = run;
        await using var scope = run.CreateScope(channel: null, specName: "spec");
        var stub = scope.Services.GetRequiredService<RecordingStub>();

        var refused = await Should.ThrowAsync<InvalidOperationException>(() => stub.Notify("payments", "webhooks/refuse", new { reference = "nope" }));

        refused.Message.ShouldContain("RecordingStub");
        refused.Message.ShouldContain("payments");
        refused.Message.ShouldContain("webhooks/refuse");
        refused.Message.ShouldContain("422");
        refused.Message.ShouldContain("Unknown payment reference");
    }

    // W3C baggage allows whitespace around '=' and ',', so it's parsed, never compared as a string.
    private static List<KeyValuePair<string, string>> Baggage(string header) =>
        header.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(member => member.Split('=', 2, StringSplitOptions.TrimEntries))
            .Select(pair => new KeyValuePair<string, string>(Uri.UnescapeDataString(pair[0]), Uri.UnescapeDataString(pair[1])))
            .ToList();

    private sealed class WebhookSuite(ReceivingSut sut) : LaminaSuite
    {
        public static async Task<(ReceivingSut Sut, LaminaRun Run)> StartAsync()
        {
            var sut = new ReceivingSut();
            var run = await new WebhookSuite(sut).StartAsync(TestContext.Current.CancellationToken);
            return (sut, run);
        }

        protected override void Configure(LaminaBuilder builder) =>
            builder
                .UseSut(s => s.AddProvider("receiving", _ => sut))
                .AddChannel("api", isDefault: true)
                .AddStub<RecordingStub>()
                .Services.AddSingleton(new List<string>());
    }
}
