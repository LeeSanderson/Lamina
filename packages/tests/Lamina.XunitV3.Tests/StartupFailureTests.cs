namespace Lamina.XunitV3.Tests;

public sealed class StartupFailureTests(SamplesRun samples) : IClassFixture<SamplesRun>
{
    [Fact]
    public void ASutThatFailsToStartFailsEverySpecWithTheSameSingleErrorAndIsNotRetried()
    {
        var results = samples.All("UnstartableSutSpecs");

        results.Count.ShouldBe(3);
        results.ShouldAllBe(r => !r.Passed);
        results.Select(r => r.FailureMessage).Distinct().ShouldHaveSingleItem()
            .ShouldBe("The UnstartableSutSuite run failed to start, so no spec can run. Start-up is not retried. The SUT refused to start (attempt 1).");
    }

    [Fact]
    public void ARunStartHookThatThrowsFailsEverySpecWithTheSameSingleErrorAndIsNotRetried()
    {
        var results = samples.All("ThrowingHookSpecs");

        results.Count.ShouldBe(2);
        results.ShouldAllBe(r => !r.Passed);
        results.Select(r => r.FailureMessage).Distinct().ShouldHaveSingleItem()
            .ShouldBe("The ThrowingHookSuite run failed to start, so no spec can run. Start-up is not retried. The run-start hook failed (attempt 1).");
    }
}
