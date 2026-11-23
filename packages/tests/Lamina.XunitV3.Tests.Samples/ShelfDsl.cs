using Lamina.Dsl;

namespace Lamina.XunitV3.Tests.Samples;

public sealed class ShelfDsl(IShelfDriver driver) : LaminaDsl
{
    public string DriverName => driver.GetType().Name;
}
