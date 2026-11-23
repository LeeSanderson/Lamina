using Lamina.XunitV3.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Lamina.XunitV3.Tests;

public sealed class ChannelGeneratorTests
{
    [Fact]
    public Task EachChannelGetsASealedSubclassCarryingItsChannelTrait()
    {
        var driver = Generate("""
            using Lamina.XunitV3;
            using Xunit;

            namespace Shop.Specs;

            internal static class ShopChannels
            {
                internal const string Api = "api";
                internal const string Web = "web";
            }

            [Channels(ShopChannels.Api, ShopChannels.Web)]
            public abstract class CheckoutSpecs
            {
                [Fact]
                public void APaidOrderIsConfirmed()
                {
                }
            }
            """);

        return Verify(driver);
    }

    [Fact]
    public Task AChannelNameThatIsNotAnIdentifierIsPascalCasedInTheClassName()
    {
        var driver = Generate("""
            [Lamina.XunitV3.Channels("mobile-app")]
            public abstract class CheckoutSpecs
            {
            }
            """);

        return Verify(driver);
    }

    [Fact]
    public void ASpecClassWithoutChannelsGetsNothing()
    {
        var driver = Generate("""
            public abstract class CheckoutSpecs
            {
            }
            """);

        driver.GetRunResult().GeneratedTrees.ShouldBeEmpty();
    }

    private static GeneratorDriver Generate(string source)
    {
        var compilation = CSharpCompilation.Create(
            "Specs",
            [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest))],
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable, generalDiagnosticOption: ReportDiagnostic.Error));

        var driver = CSharpGeneratorDriver.Create(new ChannelGenerator())
            .RunGeneratorsAndUpdateCompilation(compilation, out var output, out var generatorDiagnostics, TestContext.Current.CancellationToken);

        generatorDiagnostics.ShouldBeEmpty();
        output.GetDiagnostics(TestContext.Current.CancellationToken).Where(d => d.Severity >= DiagnosticSeverity.Warning).ShouldBeEmpty();
        return driver;
    }

    private static IEnumerable<MetadataReference> References =>
        (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string ?? "")
            .Split(Path.PathSeparator)
            .Append(typeof(ChannelsAttribute).Assembly.Location)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => MetadataReference.CreateFromFile(path));
}
