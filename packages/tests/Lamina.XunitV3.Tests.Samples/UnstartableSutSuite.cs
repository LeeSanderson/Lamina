namespace Lamina.XunitV3.Tests.Samples;

public sealed class UnstartableSutSuite : LaminaSuite
{
    private static int _starts;

    protected override void Configure(LaminaBuilder builder) =>
        builder
            .UseSut(sut => sut.AddProvider("unstartable", _ => new UnstartableSut(Interlocked.Increment(ref _starts))))
            .AddChannel("api", isDefault: true);

    private sealed class UnstartableSut(int attempt) : ISutProvider
    {
        public Task<ISystemUnderTest> StartAsync(CancellationToken ct) =>
            throw new InvalidOperationException($"The SUT refused to start (attempt {attempt}).");
    }
}
