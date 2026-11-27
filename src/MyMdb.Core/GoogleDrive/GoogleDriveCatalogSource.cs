using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using MyMdb.Core.DataSources;
using MyMdb.Core.Models;
using MyMdb.Core.Parsing;

namespace MyMdb.Core.GoogleDrive;

public sealed class GoogleDriveCatalogSource : ICatalogDataSource
{
    private static readonly string[] IndexedAssetFolders = ["Posters", "Backdrops", "Actors", "Covers"];

    private readonly GoogleOAuthClient _oauthClient;
    private readonly GoogleDriveApiClient _driveApi;
    private readonly string _rootFolderId;
    private readonly string _cacheDirectory;
    private readonly EmdbParser _parser = new();
    private readonly SemaphoreSlim _syncLock = new(1, 1);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _downloadLocks = new();

    private DriveCacheIndex? _index;

    public GoogleDriveCatalogSource(
        GoogleOAuthClient oauthClient,
        GoogleDriveApiClient driveApi,
        string rootFolderId,
        string cacheDirectory)
    {
        ArgumentNullException.ThrowIfNull(oauthClient);
        ArgumentNullException.ThrowIfNull(driveApi);
        ArgumentException.ThrowIfNullOrWhiteSpace(rootFolderId);
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheDirectory);

        _oauthClient = oauthClient;
        _driveApi = driveApi;
        _rootFolderId = rootFolderId;
        _cacheDirectory = cacheDirectory;
    }

    private string CachedDatPath => Path.Combine(_cacheDirectory, "emdb.dat");
    private string IndexPath => Path.Combine(_cacheDirectory, "drive-index.json");

    public bool RequiresAuthentication => !_oauthClient.HasSavedToken;
    public bool SupportsSignOut => true;

    public void SignOut() => _oauthClient.ClearSavedToken();

    public async Task<Catalog> LoadCatalogAsync(bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        await _syncLock.WaitAsync(cancellationToken);
        try
        {
            _index ??= LoadSavedIndex();

            if (!forceRefresh && File.Exists(CachedDatPath))
            {
                return await ParseCachedCatalogAsync(cancellationToken);
            }

            await SyncMetadataAndDatabaseAsync(cancellationToken);
            return await ParseCachedCatalogAsync(cancellationToken);
        }
        finally
        {
            _syncLock.Release();
        }
    }

    public async Task<Stream?> OpenAssetStreamAsync(
        string subdirectory,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        if (!AssetPathGuard.IsSafeSegment(subdirectory) || !AssetPathGuard.IsSafeSegment(fileName))
        {
            return null;
        }

        var localPath = Path.Combine(_cacheDirectory, subdirectory, fileName);
        if (File.Exists(localPath))
        {
            return File.OpenRead(localPath);
        }

        _index ??= LoadSavedIndex();
        var assetKey = $"{subdirectory}/{fileName}";
        if (_index is null || !_index.AssetFileIds.TryGetValue(assetKey, out var fileId))
        {
            return null;
        }

        var fileLock = _downloadLocks.GetOrAdd(assetKey, _ => new SemaphoreSlim(1, 1));
        await fileLock.WaitAsync(cancellationToken);
        try
        {
            if (File.Exists(localPath))
            {
                return File.OpenRead(localPath);
            }

            var accessToken = await _oauthClient.GetAccessTokenAsync(cancellationToken);
            await DownloadAtomicallyAsync(fileId, accessToken, localPath, cancellationToken);
            return File.Exists(localPath) ? File.OpenRead(localPath) : null;
        }
        catch
        {
            return null;
        }
        finally
        {
            fileLock.Release();
        }
    }

    private async Task SyncMetadataAndDatabaseAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_cacheDirectory);

        var accessToken = await _oauthClient.GetAccessTokenAsync(cancellationToken);
        var rootChildren = await _driveApi.ListChildrenAsync(_rootFolderId, accessToken, cancellationToken);

        var emdbFile = rootChildren.FirstOrDefault(f =>
            !f.IsFolder && string.Equals(f.Name, "emdb.dat", StringComparison.OrdinalIgnoreCase))
            ?? throw new FileNotFoundException("Could not find 'emdb.dat' in the configured Google Drive folder.");

        var needsDownload = !File.Exists(CachedDatPath)
            || !string.Equals(_index?.EmdbModifiedTime, emdbFile.ModifiedTime, StringComparison.Ordinal);

        if (needsDownload)
        {
            await DownloadAtomicallyAsync(emdbFile.Id, accessToken, CachedDatPath, cancellationToken);
        }

        var assetMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var folderMap = rootChildren
            .Where(f => f.IsFolder)
            .ToDictionary(f => f.Name, f => f.Id, StringComparer.OrdinalIgnoreCase);

        foreach (var folderName in IndexedAssetFolders)
        {
            if (!folderMap.TryGetValue(folderName, out var folderId))
            {
                continue;
            }

            var files = await _driveApi.ListChildrenAsync(folderId, accessToken, cancellationToken);
            foreach (var file in files)
            {
                if (!file.IsFolder && AssetPathGuard.IsSafeSegment(file.Name))
                {
                    assetMap[$"{folderName}/{file.Name}"] = file.Id;
                }
            }
        }

        _index = new DriveCacheIndex
        {
            EmdbFileId = emdbFile.Id,
            EmdbModifiedTime = emdbFile.ModifiedTime,
            AssetFileIds = assetMap,
        };

        SaveIndex(_index);
    }

    private Task<Catalog> ParseCachedCatalogAsync(CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var stream = File.OpenRead(CachedDatPath);
            return _parser.Parse(stream);
        }, cancellationToken);
    }

    private async Task DownloadAtomicallyAsync(
        string fileId,
        string accessToken,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        var dir = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var tempPath = $"{destinationPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var fs = File.Create(tempPath))
            {
                await _driveApi.DownloadFileAsync(fileId, accessToken, fs, cancellationToken);
            }

            File.Move(tempPath, destinationPath, overwrite: true);
        }
        catch
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            throw;
        }
    }

    private DriveCacheIndex? LoadSavedIndex()
    {
        if (!File.Exists(IndexPath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(IndexPath);
            var loaded = JsonSerializer.Deserialize<DriveCacheIndex>(json);
            if (loaded is null)
            {
                return null;
            }

            return loaded with
            {
                AssetFileIds = new Dictionary<string, string>(
                    loaded.AssetFileIds,
                    StringComparer.OrdinalIgnoreCase),
            };
        }
        catch
        {
            return null;
        }
    }

    private void SaveIndex(DriveCacheIndex index)
    {
        var tempPath = $"{IndexPath}.tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(index));
        File.Move(tempPath, IndexPath, overwrite: true);
    }

    private sealed record DriveCacheIndex
    {
        [JsonPropertyName("emdb_file_id")]
        public string? EmdbFileId { get; init; }

        [JsonPropertyName("emdb_modified_time")]
        public string? EmdbModifiedTime { get; init; }

        [JsonPropertyName("asset_file_ids")]
        public Dictionary<string, string> AssetFileIds { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
