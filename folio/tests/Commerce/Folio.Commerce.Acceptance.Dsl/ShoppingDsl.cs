using System.Diagnostics.CodeAnalysis;
using Lamina.Dsl;

namespace Folio.Commerce.Acceptance.Dsl;

[SuppressMessage("Minor Code Smell", "S4261:Methods should be named according to their synchronicities", Justification = "DSL methods read as domain language in specs.")]
public sealed class ShoppingDsl(IShoppingDriver driver) : LaminaDsl
{
    public Task AsCustomer(string name) => driver.SignInAsCustomerAsync(Alias(name));

    public Task AsStaff(string name) => driver.SignInAsStaffAsync(Alias(name));
}
