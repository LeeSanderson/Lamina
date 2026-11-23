namespace Lamina.XunitV3.Tests;

// A copy of the samples whose appsettings.local.json sets Lamina:Repeat to 3, run with the filter Channel=api.
public sealed class RepeatedApiChannelRun() : SamplesRun(filters => filters.AddIncludedTraitFilter("Channel", "api"))
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"lamina-repeat-{Guid.NewGuid():N}");

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        try
        {
            Directory.Delete(_folder, recursive: true);
        }
        catch (IOException)
        {
            // The runner may still hold a file open; the temp folder is left for the OS to clean.
        }
    }

    protected override string PrepareAssembly()
    {
        var source = Path.GetDirectoryName(SamplesAssembly)!;
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(_folder, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }

        File.WriteAllText(Path.Combine(_folder, "appsettings.local.json"), """{ "Lamina": { "Repeat": 3 } }""");
        return Path.Combine(_folder, Path.GetFileName(SamplesAssembly));
    }
}
