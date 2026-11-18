// PROTOTYPE stand-in for the `Lamina` core package. Framework-neutral: no xUnit types in here.
using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;

namespace Lamina;

/// <summary>The suite's composition root. One per suite assembly.</summary>
public abstract class LaminaSuite
{
    protected abstract void Configure(LaminaBuilder b);

    internal async Task<LaminaRun> StartAsync()
    {
        var builder = new LaminaBuilder();
        Configure(builder);
        var run = builder.Build();
        await run.Sut.WaitUntilReadyAsync(CancellationToken.None);
        return run;
    }
}

public interface ISystemUnderTest : IAsyncDisposable
{
    Uri Endpoint(string name);
    Task WaitUntilReadyAsync(CancellationToken ct);
}

public sealed class LaminaBuilder
{
    private readonly ServiceCollection services = new();
    private readonly List<string> channels = [];
    private readonly HashSet<Type> capabilities = [];
    private string? defaultChannel;
    private Func<ISystemUnderTest>? sutFactory;

    internal IServiceCollection Services => services;
    internal IReadOnlySet<Type> Capabilities => capabilities;

    public LaminaBuilder UseSut(Func<ISystemUnderTest> factory)
    {
        sutFactory = factory;
        return this;
    }

    public LaminaBuilder AddChannel(string channel, bool isDefault = false)
    {
        channels.Add(channel);
        if (isDefault) defaultChannel = channel;
        return this;
    }

    // A capability is any interface with drivers registered per channel. Drivers are keyed scoped services;
    // the un-keyed capability is a scoped factory that reads the spec scope's channel (see ResolveCapability).
    public LaminaBuilder AddDriver<TCapability, TDriver>(string channel)
        where TCapability : class
        where TDriver : class, TCapability
    {
        services.AddKeyedScoped<TCapability, TDriver>(channel);
        if (capabilities.Add(typeof(TCapability)))
        {
            services.AddScoped(sp => (TCapability)ResolveCapability(sp, typeof(TCapability), sp.GetRequiredService<SpecScope>().Channel));
        }

        return this;
    }

    // Stubs are channel-independent: one per external system, scoped per spec so its rules and journal are per spec.
    public LaminaBuilder AddStub<TStub>()
        where TStub : class
    {
        services.AddScoped<TStub>();
        return this;
    }

    // Test infrastructure the team registers by hand (fakes, HttpClients, options).
    public LaminaBuilder AddServices(Action<IServiceCollection> configure)
    {
        configure(services);
        return this;
    }

    public LaminaBuilder AddDslsFromAssemblyOf<T>()
    {
        foreach (var dslType in typeof(T).Assembly.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false } && t.IsSubclassOf(typeof(Dsl.LaminaDsl))))
        {
            services.AddScoped(dslType, sp => Dsl.LaminaDsl.Create(sp, dslType));
        }

        return this;
    }

    internal LaminaRun Build()
    {
        var channelModel = new ChannelModel(channels, defaultChannel ?? throw new InvalidOperationException("No default channel. Call AddChannel(..., isDefault: true)."));
        services.AddSingleton(channelModel);
        services.AddSingleton(this);
        services.AddScoped<SpecScopeHolder>();
        services.AddScoped(sp => sp.GetRequiredService<SpecScopeHolder>().Scope ?? throw new InvalidOperationException("No spec scope."));
        var sut = (sutFactory ?? throw new InvalidOperationException("No SUT. Call UseSut(...)."))();
        services.AddSingleton(sut);
        return new LaminaRun(services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true }), sut, channelModel);
    }

    internal static object ResolveCapability(IServiceProvider sp, Type capability, string channel)
    {
        var scope = sp.GetRequiredService<SpecScope>();
        var model = sp.GetRequiredService<ChannelModel>();
        var keyed = (IKeyedServiceProvider)sp;
        var driver = keyed.GetKeyedService(capability, channel);
        if (driver is not null) return driver;

        scope.Note($"{capability.Name} has no driver on '{channel}'; using '{model.Default}'.");
        return keyed.GetRequiredKeyedService(capability, model.Default);
    }
}

public sealed record ChannelModel(IReadOnlyList<string> All, string Default);

/// <summary>Everything that lives for the whole run: the DI root, the SUT, test-ID collision checks.</summary>
public sealed class LaminaRun(ServiceProvider root, ISystemUnderTest sut, ChannelModel channels) : IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, byte> testIds = new(StringComparer.Ordinal);

    public ISystemUnderTest Sut => sut;

    public SpecScope CreateScope(string? channel, string specName)
    {
        var serviceScope = root.CreateAsyncScope();
        var scope = new SpecScope(NewTestId(), channel ?? channels.Default, specName, serviceScope);
        serviceScope.ServiceProvider.GetRequiredService<SpecScopeHolder>().Scope = scope;
        return scope;
    }

    public async ValueTask DisposeAsync()
    {
        await root.DisposeAsync();
        await sut.DisposeAsync();
    }

    private string NewTestId()
    {
        const string alphabet = "abcdefghijklmnopqrstuvwxyz0123456789";
        while (true)
        {
            var id = RandomNumberGenerator.GetString(alphabet, 6);
            if (testIds.TryAdd(id, 0)) return id;
        }
    }
}

internal sealed class SpecScopeHolder
{
    public SpecScope? Scope { get; set; }
}

/// <summary>Per-spec context. Created by the framework adapter before each spec, disposed after it.</summary>
public sealed class SpecScope(string testId, string channel, string specName, AsyncServiceScope services) : IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, object> remembered = new(StringComparer.Ordinal);
    private readonly ConcurrentQueue<string> notes = new();

    public string TestId => testId;
    public string Channel => channel;
    public string SpecName => specName;
    public Aliases Aliases { get; } = new(testId);
    public IServiceProvider Services => services.ServiceProvider;
    public IEnumerable<string> Notes => notes;

    public void Remember<T>(Key<T> key, T value) where T : notnull => remembered[key.Name] = value;

    public T Recall<T>(Key<T> key) where T : notnull =>
        remembered.TryGetValue(key.Name, out var value) ? (T)value : throw new InvalidOperationException($"Nothing remembered as '{key.Name}'.");

    public bool TryRecall<T>(Key<T> key, [System.Diagnostics.CodeAnalysis.MaybeNullWhen(false)] out T value) where T : notnull
    {
        var found = remembered.TryGetValue(key.Name, out var stored);
        value = found ? (T)stored! : default;
        return found;
    }

    public void Note(string note) => notes.Enqueue(note);

    public ValueTask DisposeAsync() => services.DisposeAsync();
}

public sealed record Key<T>(string Name) where T : notnull;

public sealed class Aliases(string testId)
{
    private readonly ConcurrentDictionary<string, string> forward = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> backward = new(StringComparer.Ordinal);

    public string For(string name) => forward.GetOrAdd(name, n =>
    {
        var alias = $"{n}-{testId}";
        backward[alias] = n;
        return alias;
    });

    public string Original(string alias) => backward.GetValueOrDefault(alias, alias);

    public override string ToString() => string.Join(", ", forward.Select(p => $"{p.Key} -> {p.Value}"));
}

/// <summary>Stand-in only; "Eventually and failure diagnostics" designs the real one.</summary>
public static class Eventually
{
    public static async Task<T> Until<T>(Func<Task<T>> probe, Func<T, bool> condition, TimeSpan within, string because)
    {
        var deadline = DateTimeOffset.UtcNow + within;
        T last;
        do
        {
            last = await probe();
            if (condition(last)) return last;
            await Task.Delay(50);
        }
        while (DateTimeOffset.UtcNow < deadline);

        throw new TimeoutException($"Expected {because} within {within.TotalSeconds}s. Last observed: {last}");
    }
}
