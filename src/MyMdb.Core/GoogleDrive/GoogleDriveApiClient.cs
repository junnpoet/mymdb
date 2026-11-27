using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MyMdb.Core.GoogleDrive;

public sealed record DriveFileEntry
{
    public const string FolderMimeType = "application/vnd.google-apps.folder";

    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("mimeType")]
    public string? MimeType { get; init; }

    [JsonPropertyName("modifiedTime")]
    public string? ModifiedTime { get; init; }

    [JsonIgnore]
    public bool IsFolder => string.Equals(MimeType, FolderMimeType, StringComparison.Ordinal);
}

public sealed class GoogleDriveApiClient
{
    private const string FilesEndpoint = "https://www.googleapis.com/drive/v3/files";

    private readonly HttpClient _httpClient;

    public GoogleDriveApiClient(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<DriveFileEntry>> ListChildrenAsync(
        string folderId,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderId);
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);

        var results = new List<DriveFileEntry>();
        string? pageToken = null;
        var escapedId = folderId.Replace("'", "\\'", StringComparison.Ordinal);

        do
        {
            var query = $"'{escapedId}' in parents and trashed = false";
            var url = $"{FilesEndpoint}?q={Uri.EscapeDataString(query)}"
                + "&pageSize=1000"
                + "&fields=nextPageToken,files(id,name,mimeType,modifiedTime)";

            if (!string.IsNullOrEmpty(pageToken))
            {
                url += $"&pageToken={Uri.EscapeDataString(pageToken)}";
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var page = await JsonSerializer.DeserializeAsync<DriveFileListResponse>(stream, cancellationToken: cancellationToken);
            if (page?.Files is { Count: > 0 })
            {
                results.AddRange(page.Files);
            }

            pageToken = page?.NextPageToken;
        }
        while (!string.IsNullOrEmpty(pageToken));

        return results;
    }

    public async Task DownloadFileAsync(
        string fileId,
        string accessToken,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileId);
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);
        ArgumentNullException.ThrowIfNull(destination);

        var url = $"{FilesEndpoint}/{Uri.EscapeDataString(fileId)}?alt=media";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await source.CopyToAsync(destination, cancellationToken);
    }

    private sealed class DriveFileListResponse
    {
        [JsonPropertyName("nextPageToken")]
        public string? NextPageToken { get; init; }

        [JsonPropertyName("files")]
        public List<DriveFileEntry>? Files { get; init; }
    }
}
