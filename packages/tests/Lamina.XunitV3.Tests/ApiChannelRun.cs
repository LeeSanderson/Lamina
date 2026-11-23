namespace Lamina.XunitV3.Tests;

/// <summary>
/// Runs the samples with the filter <c>Channel=api</c>, as <c>dotnet test --filter "Channel=api"</c> does.
/// </summary>
public sealed class ApiChannelRun() : SamplesRun(filters => filters.AddIncludedTraitFilter("Channel", "api"));
