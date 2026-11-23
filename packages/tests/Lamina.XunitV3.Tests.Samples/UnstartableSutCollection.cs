namespace Lamina.XunitV3.Tests.Samples;

[CollectionDefinition(nameof(UnstartableSutSuite))]
public sealed class UnstartableSutCollection : ICollectionFixture<LaminaFixture<UnstartableSutSuite>>;
