using Lamina.Drivers.Http;

namespace Folio.Commerce.Acceptance.Drivers;

internal sealed class ApiSession(TestJwt jwt)
{
    private const string CustomerRole = "customer";
    private const string StaffRole = "staff";

    private string? _token;

    internal string Token => _token ?? throw new InvalidOperationException(
        "No one is signed in to the Web BFF. Sign in as a customer or staff member before acting.");

    internal void SignInAsCustomer(string subject) => _token = jwt.Mint(subject, [CustomerRole]);

    internal void SignInAsStaff(string subject) => _token = jwt.Mint(subject, [StaffRole]);
}
