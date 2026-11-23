using Folio.Commerce.Acceptance.Dsl;

namespace Folio.Commerce.Acceptance.Drivers;

internal sealed class ApiShoppingDriver(ApiSession session) : IShoppingDriver
{
    public Task SignInAsCustomerAsync(string name)
    {
        session.SignInAsCustomer(name);
        return Task.CompletedTask;
    }

    public Task SignInAsStaffAsync(string name)
    {
        session.SignInAsStaff(name);
        return Task.CompletedTask;
    }
}
