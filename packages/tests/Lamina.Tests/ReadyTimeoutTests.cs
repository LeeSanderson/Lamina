namespace Lamina.Tests;

public sealed class ReadyTimeoutTests
{
    private static readonly TimeSpan AspireDefault = TimeSpan.FromSeconds(120);

    [Fact]
    public async Task WithNothingConfiguredItIsTheProvidersDefault() =>
        (await ReadyTimeout(AspireDefault)).ShouldBe(TimeSpan.FromSeconds(120));

    [Fact]
    public async Task TheDefaultIsScaledByTheTimeoutMultiplier() =>
        (await ReadyTimeout(TimeSpan.FromSeconds(60), ("Lamina:TimeoutMultiplier", "2.5"))).ShouldBe(TimeSpan.FromSeconds(150));

    [Fact]
    public async Task AConfiguredTimeSpanReplacesTheDefaultAndIsScaled() =>
        (await ReadyTimeout(AspireDefault, ("Lamina:Sut:ReadyTimeout", "00:00:30"), ("Lamina:TimeoutMultiplier", "2"))).ShouldBe(TimeSpan.FromSeconds(60));

    [Fact]
    public async Task APlainNumberIsReadAsSeconds() =>
        (await ReadyTimeout(AspireDefault, ("Lamina:Sut:ReadyTimeout", "45"))).ShouldBe(TimeSpan.FromSeconds(45));

    private static async Task<TimeSpan> ReadyTimeout(TimeSpan providerDefault, params (string Key, string Value)[] settings)
    {
        TimeSpan? timeout = null;
        await using var run = await TestSuite.StartAsync(b =>
        {
            foreach (var (key, value) in settings)
            {
                b.Configuration[key] = value;
            }

            b.UseSut(sut => timeout = sut.ReadyTimeout(providerDefault));
        });
        return timeout.ShouldNotBeNull();
    }
}
