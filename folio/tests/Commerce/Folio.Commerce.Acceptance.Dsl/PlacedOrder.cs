namespace Folio.Commerce.Acceptance.Dsl;

/// <summary>
/// An order as its customer sees it: its status, and why it was rejected, if it was.
/// </summary>
public sealed record PlacedOrder(string Status, string? Reason);
