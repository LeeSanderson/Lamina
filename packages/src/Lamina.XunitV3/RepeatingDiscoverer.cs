using System.Globalization;
using Xunit.Sdk;
using Xunit.v3;

namespace Lamina.XunitV3;

/// <summary>
/// xUnit's discoverer, which hands on <c>copies</c> copies of each test case it finds. A copy is the same test case with
/// <c> [copy k/N]</c> on its display name and a unique ID computed as if the copy number were a theory argument, so copies
/// never collapse into one test. Traits, skips, timeouts and explicitness carry over. Test cases that aren't plain
/// <see cref="XunitTestCase"/>s (such as theories enumerated at run time) are handed on once.
/// </summary>
internal sealed class RepeatingDiscoverer(IXunitTestAssembly testAssembly, int copies) : XunitTestFrameworkDiscoverer(testAssembly)
{
    protected override ValueTask<bool> FindTestsForMethod(
        IXunitTestMethod testMethod,
        ITestFrameworkDiscoveryOptions discoveryOptions,
        Func<ITestCase, ValueTask<bool>> discoveryCallback)
    {
        if (copies == 1)
        {
            return base.FindTestsForMethod(testMethod, discoveryOptions, discoveryCallback);
        }

        return base.FindTestsForMethod(testMethod, discoveryOptions, async testCase =>
        {
            if (testCase is not XunitTestCase plain || plain.GetType() != typeof(XunitTestCase))
            {
                return await discoveryCallback(testCase).ConfigureAwait(false);
            }

            for (var copy = 1; copy <= copies; copy++)
            {
                if (!await discoveryCallback(CopyOf(plain, copy)).ConfigureAwait(false))
                {
                    return false;
                }
            }

            return true;
        });
    }

    private XunitTestCase CopyOf(XunitTestCase original, int copy) =>
        new(
            original.TestMethod,
            string.Create(CultureInfo.InvariantCulture, $"{original.TestCaseDisplayName} [copy {copy}/{copies}]"),
            UniqueIDGenerator.ForTestCase(original.UniqueID, null, [copy, copies]),
            original.Explicit,
            original.TestLabel,
            original.DisableParallelization,
            original.SkipExceptions,
            original.SkipReason,
            original.SkipType,
            original.SkipUnless,
            original.SkipWhen,
            original.Traits.ToDictionary(t => t.Key, t => new HashSet<string>(t.Value, StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase),
            original.TestMethodArguments,
            original.SourceFilePath,
            original.SourceLineNumber,
            original.Timeout);
}
