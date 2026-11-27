using System.Net;
using System.Text;
using System.Text.Json;
using MyMdb.Core.GoogleDrive;

namespace MyMdb.Core.Tests;

public sealed class GoogleDriveCatalogSourceTests : IDisposable
{
    private const char Rs = '\u001e';
    private readonly string _tempRoot;

    public GoogleDriveCatalogSourceTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"mymdb-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task LoadCatalogAndAssets_UsesCacheAndSkipsUnchangedDownloads()
    {
        var emdbBytes = BuildSampleEmdbBytes();
        var posterBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x01, 0x02 };

        var datDownloadCount = 0;
        var posterDownloadCount = 0;
        var totalHttpCalls = 0;

        var handler = new StubHttpMessageHandler(request =>
        {
            totalHttpCalls++;
            var uri = Uri.UnescapeDataString(request.RequestUri!.AbsoluteUri);

            if (uri.Contains("'root-folder'", StringComparison.Ordinal))
            {
                return JsonResponse(new
                {
                    files = new object[]
                    {
                        new { id = "file-dat", name = "emdb.dat", mimeType = "application/octet-stream", modifiedTime = "2026-11-26T10:00:00Z" },
                        new { id = "folder-posters", name = "Posters", mimeType = DriveFileEntry.FolderMimeType },
                    },
                });
            }

            if (uri.Contains("'folder-posters'", StringComparison.Ordinal))
            {
                return JsonResponse(new
                {
                    files = new object[]
                    {
                        new { id = "file-poster-1", name = "000001.jpg", mimeType = "image/jpeg" },
                    },
                });
            }

            if (uri.Contains("/files/file-dat?alt=media", StringComparison.Ordinal))
            {
                datDownloadCount++;
                return BytesResponse(emdbBytes);
            }

            if (uri.Contains("/files/file-poster-1?alt=media", StringComparison.Ordinal))
            {
                posterDownloadCount++;
                return BytesResponse(posterBytes);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var httpClient = new HttpClient(handler);
        var tokenPath = Path.Combine(_tempRoot, "token.json");
        File.WriteAllText(tokenPath, JsonSerializer.Serialize(new GoogleOAuthToken
        {
            AccessToken = "test-access-token",
            RefreshToken = "test-refresh-token",
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(1),
        }));

        var oauth = new GoogleOAuthClient(
            httpClient,
            new GoogleOAuthConfig { ClientId = "test-client" },
            tokenPath,
            (_, _, _) => throw new InvalidOperationException("Should not prompt when token is valid."));

        var source = new GoogleDriveCatalogSource(
            oauth,
            new GoogleDriveApiClient(httpClient),
            "root-folder",
            Path.Combine(_tempRoot, "cache"));

        // 1. First load downloads emdb.dat and builds asset index
        var catalog1 = await source.LoadCatalogAsync();
        Assert.Single(catalog1.Entries);
        Assert.Equal("Dune", catalog1.Entries[0].Title);
        Assert.Equal(1, datDownloadCount);

        // 2. Second load without forceRefresh uses local cache with 0 HTTP calls
        var callsAfterFirstLoad = totalHttpCalls;
        var catalog2 = await source.LoadCatalogAsync(forceRefresh: false);
        Assert.Single(catalog2.Entries);
        Assert.Equal(callsAfterFirstLoad, totalHttpCalls);

        // 3. Force refresh with unchanged modifiedTime skips re-downloading emdb.dat
        await source.LoadCatalogAsync(forceRefresh: true);
        Assert.Equal(1, datDownloadCount);

        // 4. First asset request downloads poster; second serves from disk cache
        await using (var s1 = await source.OpenAssetStreamAsync("Posters", "000001.jpg"))
        {
            Assert.NotNull(s1);
            using var ms = new MemoryStream();
            await s1!.CopyToAsync(ms);
            Assert.Equal(posterBytes, ms.ToArray());
        }

        await using (var s2 = await source.OpenAssetStreamAsync("Posters", "000001.jpg"))
        {
            Assert.NotNull(s2);
        }

        Assert.Equal(1, posterDownloadCount);

        // 5. Path traversal attempts are rejected
        Assert.Null(await source.OpenAssetStreamAsync("Posters", "../emdb.dat"));
        Assert.Null(await source.OpenAssetStreamAsync("..", "token.json"));
    }

    private static byte[] BuildSampleEmdbBytes()
    {
        var json = $$"""
        {
            "version": "80",
            "movies": [{
                "title": "Dune{{Rs}}{{Rs}}Legendary{{Rs}}-1",
                "year": "2021{{Rs}}{{Rs}}155{{Rs}}#US{{Rs}}0{{Rs}}0{{Rs}}@0{{Rs}}{{Rs}}-1{{Rs}}256{{Rs}}1{{Rs}}0{{Rs}}0{{Rs}}0{{Rs}}1{{Rs}}{{Rs}}",
                "genres": "S{{Rs}}1160419|0{{Rs}}20240415{{Rs}}{{Rs}}1@0@0{{Rs}}{{Rs}}0{{Rs}}{{Rs}}20240415{{Rs}}{{Rs}}438631",
                "cast": "{{Rs}}",
                "plot": "Desert planet.",
                "tagline": "{{Rs}}{{Rs}}",
                "rating": "438{{Rs}}00{{Rs}}00{{Rs}}00{{Rs}}00{{Rs}}E{{Rs}}{{Rs}}1000{{Rs}}0{{Rs}}999999074{{Rs}}"
            }]
        }
        """;

        var preamble = Encoding.Unicode.GetPreamble();
        var body = Encoding.Unicode.GetBytes(json);
        var buffer = new byte[preamble.Length + body.Length];
        Buffer.BlockCopy(preamble, 0, buffer, 0, preamble.Length);
        Buffer.BlockCopy(body, 0, buffer, preamble.Length, body.Length);
        return buffer;
    }

    private static HttpResponseMessage JsonResponse(object payload) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
        };

    private static HttpResponseMessage BytesResponse(byte[] bytes) =>
        new(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(bytes),
        };

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(responder(request));
    }
}
