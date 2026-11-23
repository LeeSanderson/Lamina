using Microsoft.Extensions.DependencyInjection;

namespace Lamina.Tests;

public sealed class DriverTests
{
    private interface IShoppingDriver;

    [Fact]
    public async Task ACapabilityResolvesToTheDriverForTheSpecsChannel()
    {
        await using var run = await TestSuite.StartAsync(b => b
            .AddChannel("web")
            .AddDriver<IShoppingDriver, ApiShoppingDriver>("api")
            .AddDriver<IShoppingDriver, WebShoppingDriver>("web"));
        await using var api = run.CreateScope(channel: "api", specName: "spec");
        await using var web = run.CreateScope(channel: "web", specName: "spec");

        api.Services.GetRequiredService<IShoppingDriver>().ShouldBeOfType<ApiShoppingDriver>();
        web.Services.GetRequiredService<IShoppingDriver>().ShouldBeOfType<WebShoppingDriver>();
        web.Summary.ShouldBe($"Test {web.TestId} on 'web' · aliases: none");
    }

    [Fact]
    public async Task ACapabilityWithNoDriverForTheSpecsChannelFallsBackToTheDefaultChannelAndTheFooterSaysSo()
    {
        await using var run = await TestSuite.StartAsync(b => b
            .AddChannel("web")
            .AddDriver<IShoppingDriver, ApiShoppingDriver>("api"));
        await using var web = run.CreateScope(channel: "web", specName: "spec");

        web.Services.GetRequiredService<IShoppingDriver>().ShouldBeOfType<ApiShoppingDriver>();
        web.Summary.ShouldBe($"Test {web.TestId} on 'web' · aliases: none · fell back: IShoppingDriver has no 'web' driver, so it ran on 'api'");
    }

    [Fact]
    public async Task ACapabilityWithNoDriverForItsChannelOrTheDefaultChannelIsRefused()
    {
        await using var run = await TestSuite.StartAsync(b => b
            .AddChannel("web")
            .AddChannel("mobile")
            .AddDriver<IShoppingDriver, WebShoppingDriver>("web"));
        await using var mobile = run.CreateScope(channel: "mobile", specName: "spec");

        Should.Throw<InvalidOperationException>(() => mobile.Services.GetRequiredService<IShoppingDriver>())
            .Message.ShouldBe("IShoppingDriver has no driver for 'mobile' or for the default channel 'api'. Drivers: web.");
    }

    [Fact]
    public async Task EachSpecGetsItsOwnDriver()
    {
        await using var run = await TestSuite.StartAsync(b => b.AddDriver<IShoppingDriver, ApiShoppingDriver>("api"));
        await using var first = run.CreateScope(channel: null, specName: "first");
        await using var second = run.CreateScope(channel: null, specName: "second");

        first.Services.GetRequiredService<IShoppingDriver>()
            .ShouldBeSameAs(first.Services.GetRequiredService<IShoppingDriver>());
        first.Services.GetRequiredService<IShoppingDriver>()
            .ShouldNotBeSameAs(second.Services.GetRequiredService<IShoppingDriver>());
    }

    private sealed class ApiShoppingDriver : IShoppingDriver;

    private sealed class WebShoppingDriver : IShoppingDriver;
}
