using MyMdb.Core.Models;

namespace MyMdb.Core.DataSources;

public interface ICatalogDataSource
{
    Task<Catalog> LoadCatalogAsync(bool forceRefresh = false, CancellationToken cancellationToken = default);
    Task<Stream?> OpenAssetStreamAsync(string subdirectory, string fileName, CancellationToken cancellationToken = default);
}
