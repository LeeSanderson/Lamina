namespace Lamina.XunitV3.Tests.Samples;

[Channels("api", "web")]
public abstract class RepeatedSpecs : SampleSpec
{
    [Fact]
    public void EachCopyWritesItsXunitTestCaseId()
    {
        var testCase = TestContext.Current.TestCase;

        Assert.NotNull(testCase);
        TestContext.Current.TestOutputHelper!.WriteLine($"xUnit test case {testCase.UniqueID}");
    }
}
