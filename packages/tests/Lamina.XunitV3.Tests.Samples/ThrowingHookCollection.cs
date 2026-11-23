namespace Lamina.XunitV3.Tests.Samples;

[CollectionDefinition(nameof(ThrowingHookSuite))]
public sealed class ThrowingHookCollection : ICollectionFixture<LaminaFixture<ThrowingHookSuite>>;
