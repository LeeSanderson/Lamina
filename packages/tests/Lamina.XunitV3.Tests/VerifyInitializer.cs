using System.Runtime.CompilerServices;

namespace Lamina.XunitV3.Tests;

public static class VerifyInitializer
{
    [ModuleInitializer]
    public static void Initialize() => VerifySourceGenerators.Initialize();
}
