using Microsoft.Extensions.DependencyInjection;

namespace Lamina.XunitV3.Tests.Samples;

public sealed class ThrowingHookSuite : LaminaSuite
{
    private static int _runs;

    protected override void Configure(LaminaBuilder builder) =>
        builder
            .UseSut(sut => sut.AddProvider("fake", _ => new FakeSut()))
            .AddChannel("api", isDefault: true)
            .Services.AddSingleton<IRunStartHook>(_ => new ThrowingHook(Interlocked.Increment(ref _runs)));

    private sealed class ThrowingHook(int attempt) : IRunStartHook
    {
        public Task OnRunStartAsync(ISystemUnderTest sut, CancellationToken ct) =>
            throw new InvalidOperationException($"The run-start hook failed (attempt {attempt}).");
    }
}
