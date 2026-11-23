namespace Lamina.Stubs.WireMock.Tests;

public sealed class DependencyTests
{
    [Theory]
    [InlineData("RestEase")]
    [InlineData("Newtonsoft.Json")]
    [InlineData("WireMock.Net")]
    public void ThePackageDoesNotReference(string assembly) =>
        typeof(WireMockStub).Assembly.GetReferencedAssemblies()
            .Select(a => a.Name)
            .ShouldNotContain(name => name != null && name.StartsWith(assembly, StringComparison.Ordinal));
}
