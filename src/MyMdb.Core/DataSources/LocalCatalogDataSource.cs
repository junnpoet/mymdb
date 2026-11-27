using MyMdb.Core.Models;
using MyMdb.Core.Parsing;

namespace MyMdb.Core.DataSources;

public sealed class LocalCatalogDataSource : ICatalogDataSource
{
    private readonly EmdbParser _parser = new();

    public string BaseDirectory { get; }
    public bool RequiresAuthentication => false;
    public bool SupportsSignOut => false;
    public void SignOut() { }

    public LocalCatalogDataSource(string baseDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);
        BaseDirectory = baseDirectory;
    }

    public Task<Catalog> LoadCatalogAsync(bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var datPath = Path.Combine(BaseDirectory, "emdb.dat");
            if (!File.Exists(datPath))
            {
                throw new FileNotFoundException($"EMDB database not found at '{datPath}'.", datPath);
            }

            using var stream = File.OpenRead(datPath);
            return _parser.Parse(stream);
        }, cancellationToken);
    }

    public Task<Stream?> OpenAssetStreamAsync(
        string subdirectory,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        if (!AssetPathGuard.IsSafeSegment(subdirectory) || !AssetPathGuard.IsSafeSegment(fileName))
        {
            return Task.FromResult<Stream?>(null);
        }

        var fullPath = Path.Combine(BaseDirectory, subdirectory, fileName);
        if (!File.Exists(fullPath))
        {
            return Task.FromResult<Stream?>(null);
        }

        return Task.FromResult<Stream?>(File.OpenRead(fullPath));
    }
}

internal static class AssetPathGuard
{
    public static bool IsSafeSegment(string? segment) =>
        !string.IsNullOrWhiteSpace(segment)
        && segment is not "." and not ".."
        && !segment.Contains('/')
        && !segment.Contains('\\')
        && Path.GetFileName(segment) == segment;
}
