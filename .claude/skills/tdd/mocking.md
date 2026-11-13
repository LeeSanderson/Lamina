# When to Mock

Mock at **system boundaries** only:

- External APIs (payment, email, etc.)
- Databases (sometimes - prefer a real test DB, e.g. Testcontainers or SQLite in-memory)
- Time/randomness (inject `TimeProvider`; use `FakeTimeProvider` in tests)
- File system (sometimes)

Don't mock:

- Your own classes/modules
- Internal collaborators
- Anything you control

Use **NSubstitute** for the boundaries you do mock.

## Designing for Mockability

At system boundaries, design interfaces that are easy to mock:

**1. Use dependency injection**

Pass external dependencies in through the constructor rather than creating them internally:

```csharp
// Easy to mock
public class PaymentProcessor(IPaymentClient paymentClient)
{
    public Task<ChargeResult> Process(Order order) =>
        paymentClient.Charge(order.Total);
}

// Hard to mock
public class PaymentProcessor
{
    public Task<ChargeResult> Process(Order order)
    {
        var client = new StripeClient(Environment.GetEnvironmentVariable("STRIPE_KEY"));
        return client.Charge(order.Total);
    }
}
```

In the test:

```csharp
var paymentClient = Substitute.For<IPaymentClient>();
paymentClient.Charge(Arg.Any<decimal>()).Returns(ChargeResult.Approved);

var result = await new PaymentProcessor(paymentClient).Process(order);

result.Should().Be(ChargeResult.Approved);
```

**2. Prefer SDK-style interfaces over generic fetchers**

Create specific methods for each external operation instead of one generic method with conditional logic:

```csharp
// GOOD: Each method is independently mockable
public interface IShopApi
{
    Task<User> GetUser(UserId id);
    Task<IReadOnlyList<Order>> GetOrders(UserId userId);
    Task<Order> CreateOrder(NewOrder order);
}

// BAD: Mocking requires conditional logic inside the mock
public interface IShopApi
{
    Task<HttpResponseMessage> Send(string endpoint, HttpMethod method, object? body = null);
}
```

The SDK approach means:
- Each substitute returns one specific shape
- No conditional logic in test setup (no `Arg.Is<string>(e => e.StartsWith("/users"))` branching)
- Easier to see which endpoints a test exercises
- Type safety per endpoint
