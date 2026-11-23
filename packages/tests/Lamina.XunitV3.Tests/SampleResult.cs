namespace Lamina.XunitV3.Tests;

public sealed record SampleResult(string TypeName, string MethodName, bool Passed, string Output, string? FailureMessage);
