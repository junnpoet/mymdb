using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using MyMdb.Core.DataSources;
using MyMdb.Core.GoogleDrive;
using MyMdb.Core.Models;

namespace MyMdb.app.Services;

public sealed class CatalogService
{
    public const string DefaultDriveFolderId = "13DtMNl_5S0DWPuN7KMFMAYJp0pg9PASl";
    private const string DefaultLocalEmdbDirectory = "/mnt/Datos/Gdrive/Personal/Contenido/BD EMDB";

    private static readonly HttpClient SharedHttpClient = new();
    private static Func<CatalogService>? _platformFactory;

    private readonly ICatalogDataSource _dataSource;
    private readonly ConcurrentDictionary<string, Bitmap?> _imageCache = new();

    public string SourceLabel { get; }
    public bool RequiresAuthentication => _dataSource.RequiresAuthentication;
    public bool SupportsSignOut => _dataSource.SupportsSignOut;

    public void SignOut() => _dataSource.SignOut();

    public CatalogService(ICatalogDataSource dataSource, string sourceLabel)
    {
        _dataSource = dataSource;
        SourceLabel = sourceLabel;
    }

    public static void RegisterPlatformFactory(Func<CatalogService> factory)
    {
        _platformFactory = factory;
    }

    public static CatalogService CreateGoogleDrive(
        GoogleOAuthConfig config,
        string folderId,
        string tokenPath,
        string cacheDir,
        InteractiveAuthorizationHandler authorizeHandler)
    {
        var oauth = new GoogleOAuthClient(SharedHttpClient, config, tokenPath, authorizeHandler);
        var api = new GoogleDriveApiClient(SharedHttpClient);
        var source = new GoogleDriveCatalogSource(oauth, api, folderId, cacheDir);
        return new CatalogService(source, "Google Drive");
    }

    public static CatalogService CreateDefault()
    {
        if (_platformFactory is not null)
        {
            return _platformFactory();
        }

        var clientJsonPath = ResolveClientJsonPath();
        if (clientJsonPath is not null)
        {
            var config = GoogleOAuthConfig.LoadFromClientJson(clientJsonPath);
            var folderId = Environment.GetEnvironmentVariable("MYMDB_GDRIVE_FOLDER_ID")
                ?? DefaultDriveFolderId;

            var appDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MyMDB");
            var cacheDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".cache",
                "MyMDB");
            var tokenPath = Path.Combine(appDataDir, "gdrive-token.json");

            return CreateGoogleDrive(config, folderId, tokenPath, cacheDir, DesktopOAuthBroker.AuthorizeAsync);
        }

        var localDir = Environment.GetEnvironmentVariable("MYMDB_EMDB_DIR")
            ?? DefaultLocalEmdbDirectory;
        return new CatalogService(new LocalCatalogDataSource(localDir), "Local");
    }

    public Task<Catalog> LoadCatalogAsync(bool forceRefresh = false, CancellationToken cancellationToken = default) =>
        _dataSource.LoadCatalogAsync(forceRefresh, cancellationToken);

    public async Task<Bitmap?> LoadImageAsync(
        string subdirectory,
        string? fileName,
        int decodeWidth,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        var cacheKey = $"{subdirectory}/{fileName}@{decodeWidth}";
        if (_imageCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        try
        {
            await using var stream = await _dataSource.OpenAssetStreamAsync(subdirectory, fileName, cancellationToken);
            if (stream is null)
            {
                _imageCache[cacheKey] = null;
                return null;
            }

            var bitmap = await Task.Run(() => Bitmap.DecodeToWidth(stream, decodeWidth), cancellationToken);
            _imageCache[cacheKey] = bitmap;
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    private static string? ResolveClientJsonPath()
    {
        var envPath = Environment.GetEnvironmentVariable("MYMDB_GDRIVE_CLIENT_JSON");
        if (!string.IsNullOrWhiteSpace(envPath) && File.Exists(envPath))
        {
            return envPath;
        }

        var configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MyMDB",
            "client.json");
        if (File.Exists(configPath))
        {
            return configPath;
        }

        var homeConfigPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".config",
            "MyMDB",
            "client.json");
        return File.Exists(homeConfigPath) ? homeConfigPath : null;
    }
}
