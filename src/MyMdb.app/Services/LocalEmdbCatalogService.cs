using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using MyMdb.Core.Models;
using MyMdb.Core.Parsing;

namespace MyMdb.app.Services;

public sealed class LocalEmdbCatalogService
{
    private const string DefaultEmdbDirectory = "/mnt/Datos/Gdrive/Personal/Contenido/BD EMDB";

    private readonly EmdbParser _parser = new();
    private readonly ConcurrentDictionary<string, Bitmap?> _imageCache = new();

    public string BaseDirectory { get; }

    public LocalEmdbCatalogService(string? baseDirectory = null)
    {
        BaseDirectory = baseDirectory
            ?? Environment.GetEnvironmentVariable("MYMDB_EMDB_DIR")
            ?? DefaultEmdbDirectory;
    }

    public Task<Catalog> LoadCatalogAsync(CancellationToken cancellationToken = default)
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

    public Task<Bitmap?> LoadImageAsync(
        string subdirectory,
        string? fileName,
        int decodeWidth,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return Task.FromResult<Bitmap?>(null);
        }

        var cacheKey = $"{subdirectory}/{fileName}@{decodeWidth}";
        if (_imageCache.TryGetValue(cacheKey, out var cached))
        {
            return Task.FromResult(cached);
        }

        var fullPath = Path.Combine(BaseDirectory, subdirectory, fileName);
        if (!File.Exists(fullPath))
        {
            _imageCache[cacheKey] = null;
            return Task.FromResult<Bitmap?>(null);
        }

        return Task.Run<Bitmap?>(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using var stream = File.OpenRead(fullPath);
                var bitmap = Bitmap.DecodeToWidth(stream, decodeWidth);
                _imageCache[cacheKey] = bitmap;
                return bitmap;
            }
            catch
            {
                _imageCache[cacheKey] = null;
                return null;
            }
        }, cancellationToken);
    }
}
