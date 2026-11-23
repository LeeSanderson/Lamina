namespace Folio.Commerce.Acceptance.Dsl;

/// <summary>
/// Signs in to Folio. Later calls on any driver act as the signed-in user.
/// </summary>
public interface IShoppingDriver
{
    Task SignInAsCustomerAsync(string name);

    Task SignInAsStaffAsync(string name);
}
