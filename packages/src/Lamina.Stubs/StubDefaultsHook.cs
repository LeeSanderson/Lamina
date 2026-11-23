using Microsoft.Extensions.DependencyInjection;

namespace Lamina.Stubs;

internal sealed class StubDefaultsHook<TStub>(IServiceScopeFactory scopes, StubContext context) : IRunStartHook
    where TStub : LaminaStub
{
    public async Task OnRunStartAsync(ISystemUnderTest sut, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        await using var stub = ActivatorUtilities.CreateInstance<TStub>(scope.ServiceProvider);
        stub.Attach(scope: null, context);
        await stub.InstallDefaultsAtRunStartAsync().ConfigureAwait(false);
    }
}
