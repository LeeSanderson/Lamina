namespace Lamina.XunitV3.Tests;

public sealed record SampleResult(
    string TypeName,
    string MethodName,
    string DisplayName,
    bool Passed,
    string Output,
    string? FailureMessage,
    IReadOnlyDictionary<string, IReadOnlyCollection<string>> Traits);
