using Aspire.Hosting.Testing;

namespace Lamina.Sut.Aspire;

internal sealed class AspireSutProvider<TAppHost>(string[] args, TimeSpan readyTimeout) : ISutProvider
    where TAppHost : class
{
    public async Task<ISystemUnderTest> StartAsync(CancellationToken ct)
    {
        var builder = await DistributedApplicationTestingBuilder.CreateAsync<TAppHost>(args, ct).ConfigureAwait(false);
        try
        {
            var app = await builder.BuildAsync(ct).ConfigureAwait(false);
            try
            {
                await app.StartAsync(ct).ConfigureAwait(false);
            }
            catch
            {
                await app.DisposeAsync().ConfigureAwait(false);
                throw;
            }

            return new AspireSut(app, builder, args, readyTimeout);
        }
        catch
        {
            await builder.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }
}
