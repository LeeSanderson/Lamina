namespace Lamina.XunitV3.Tests;

// As dotnet test --filter "Channel=api" does.
public sealed class ApiChannelRun() : SamplesRun(filters => filters.AddIncludedTraitFilter("Channel", "api"));
