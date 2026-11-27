using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MyMdb.Core.GoogleDrive;

public sealed record GoogleOAuthConfig
{
    public required string ClientId { get; init; }
    public string? ClientSecret { get; init; }
    public string AuthUri { get; init; } = "https://accounts.google.com/o/oauth2/v2/auth";
    public string TokenUri { get; init; } = "https://oauth2.googleapis.com/token";

    public static GoogleOAuthConfig LoadFromClientJson(string path)
    {
        using var stream = File.OpenRead(path);
        using var doc = JsonDocument.Parse(stream);

        var root = doc.RootElement;
        if (!root.TryGetProperty("installed", out var section)
            && !root.TryGetProperty("web", out section))
        {
            throw new InvalidDataException("Invalid Google OAuth client.json: missing 'installed' or 'web' section.");
        }

        var clientId = section.GetProperty("client_id").GetString()
            ?? throw new InvalidDataException("Missing 'client_id' in client.json.");
        var clientSecret = section.TryGetProperty("client_secret", out var secretProp)
            ? secretProp.GetString()
            : null;
        var authUri = section.TryGetProperty("auth_uri", out var authProp) && authProp.GetString() is { Length: > 0 } a
            ? a
            : "https://accounts.google.com/o/oauth2/v2/auth";
        var tokenUri = section.TryGetProperty("token_uri", out var tokenProp) && tokenProp.GetString() is { Length: > 0 } t
            ? t
            : "https://oauth2.googleapis.com/token";

        return new GoogleOAuthConfig
        {
            ClientId = clientId,
            ClientSecret = clientSecret,
            AuthUri = authUri,
            TokenUri = tokenUri,
        };
    }
}

public sealed record GoogleOAuthToken
{
    [JsonPropertyName("access_token")]
    public required string AccessToken { get; init; }

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; init; }

    [JsonPropertyName("expires_at_utc")]
    public DateTimeOffset ExpiresAtUtc { get; init; }
}

public delegate Task<OAuthAuthorizationResult> InteractiveAuthorizationHandler(
    Uri authorizationUri,
    string expectedState,
    CancellationToken cancellationToken);

public sealed record OAuthAuthorizationResult(string Code, string RedirectUri);

public sealed class GoogleOAuthClient
{
    public const string DriveReadOnlyScope = "https://www.googleapis.com/auth/drive.readonly";

    private readonly HttpClient _httpClient;
    private readonly GoogleOAuthConfig _config;
    private readonly string _tokenFilePath;
    private readonly InteractiveAuthorizationHandler _authorizeHandler;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);

    private GoogleOAuthToken? _cachedToken;

    public bool HasSavedToken => _cachedToken is not null || File.Exists(_tokenFilePath);

    public GoogleOAuthClient(
        HttpClient httpClient,
        GoogleOAuthConfig config,
        string tokenFilePath,
        InteractiveAuthorizationHandler authorizeHandler)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenFilePath);
        ArgumentNullException.ThrowIfNull(authorizeHandler);

        _httpClient = httpClient;
        _config = config;
        _tokenFilePath = tokenFilePath;
        _authorizeHandler = authorizeHandler;
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        await _tokenLock.WaitAsync(cancellationToken);
        try
        {
            _cachedToken ??= LoadSavedToken();

            if (_cachedToken is not null && _cachedToken.ExpiresAtUtc > DateTimeOffset.UtcNow.AddMinutes(2))
            {
                return _cachedToken.AccessToken;
            }

            if (_cachedToken is { RefreshToken.Length: > 0 } expired)
            {
                var refreshed = await TryRefreshTokenAsync(expired.RefreshToken, cancellationToken);
                if (refreshed is not null)
                {
                    _cachedToken = refreshed;
                    SaveToken(refreshed);
                    return refreshed.AccessToken;
                }
            }

            var fresh = await RunInteractivePkceFlowAsync(cancellationToken);
            _cachedToken = fresh;
            SaveToken(fresh);
            return fresh.AccessToken;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    public void ClearSavedToken()
    {
        _cachedToken = null;
        if (File.Exists(_tokenFilePath))
        {
            File.Delete(_tokenFilePath);
        }
    }

    private async Task<GoogleOAuthToken> RunInteractivePkceFlowAsync(CancellationToken cancellationToken)
    {
        var codeVerifier = GenerateRandomBase64Url(32);
        var codeChallenge = ComputeSha256Base64Url(codeVerifier);
        var state = GenerateRandomBase64Url(24);

        var placeholderUri = BuildAuthorizationUri(
            redirectUri: "{REDIRECT_URI}",
            codeChallenge: codeChallenge,
            state: state);

        var authResult = await _authorizeHandler(placeholderUri, state, cancellationToken);

        var form = new Dictionary<string, string>
        {
            ["code"] = authResult.Code,
            ["client_id"] = _config.ClientId,
            ["redirect_uri"] = authResult.RedirectUri,
            ["grant_type"] = "authorization_code",
            ["code_verifier"] = codeVerifier,
        };

        if (!string.IsNullOrEmpty(_config.ClientSecret))
        {
            form["client_secret"] = _config.ClientSecret;
        }

        return await ExchangeTokenAsync(form, existingRefreshToken: null, cancellationToken);
    }

    private async Task<GoogleOAuthToken?> TryRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var form = new Dictionary<string, string>
        {
            ["refresh_token"] = refreshToken,
            ["client_id"] = _config.ClientId,
            ["grant_type"] = "refresh_token",
        };

        if (!string.IsNullOrEmpty(_config.ClientSecret))
        {
            form["client_secret"] = _config.ClientSecret;
        }

        try
        {
            return await ExchangeTokenAsync(form, existingRefreshToken: refreshToken, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    private async Task<GoogleOAuthToken> ExchangeTokenAsync(
        Dictionary<string, string> form,
        string? existingRefreshToken,
        CancellationToken cancellationToken)
    {
        using var content = new FormUrlEncodedContent(form);
        using var response = await _httpClient.PostAsync(_config.TokenUri, content, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var payload = await JsonSerializer.DeserializeAsync<TokenEndpointResponse>(stream, cancellationToken: cancellationToken)
            ?? throw new InvalidDataException("Empty token response from Google OAuth endpoint.");

        if (string.IsNullOrEmpty(payload.AccessToken))
        {
            throw new InvalidDataException("Missing access_token in Google OAuth response.");
        }

        var expiresIn = payload.ExpiresIn > 0 ? payload.ExpiresIn : 3600;
        return new GoogleOAuthToken
        {
            AccessToken = payload.AccessToken,
            RefreshToken = string.IsNullOrEmpty(payload.RefreshToken) ? existingRefreshToken : payload.RefreshToken,
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(expiresIn),
        };
    }

    public Uri BuildAuthorizationUri(string redirectUri, string codeChallenge, string state)
    {
        var query = string.Join("&", new[]
        {
            $"client_id={Uri.EscapeDataString(_config.ClientId)}",
            $"redirect_uri={Uri.EscapeDataString(redirectUri)}",
            "response_type=code",
            $"scope={Uri.EscapeDataString(DriveReadOnlyScope)}",
            $"code_challenge={Uri.EscapeDataString(codeChallenge)}",
            "code_challenge_method=S256",
            $"state={Uri.EscapeDataString(state)}",
            "access_type=offline",
            "prompt=consent",
        });

        return new Uri($"{_config.AuthUri}?{query}");
    }

    private GoogleOAuthToken? LoadSavedToken()
    {
        if (!File.Exists(_tokenFilePath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(_tokenFilePath);
            return JsonSerializer.Deserialize<GoogleOAuthToken>(json);
        }
        catch
        {
            return null;
        }
    }

    private void SaveToken(GoogleOAuthToken token)
    {
        var dir = Path.GetDirectoryName(_tokenFilePath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var tempPath = $"{_tokenFilePath}.tmp";
        var json = JsonSerializer.Serialize(token);
        File.WriteAllText(tempPath, json);

        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(tempPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        File.Move(tempPath, _tokenFilePath, overwrite: true);
    }

    private static string GenerateRandomBase64Url(int byteCount)
    {
        var bytes = RandomNumberGenerator.GetBytes(byteCount);
        return Base64UrlEncode(bytes);
    }

    private static string ComputeSha256Base64Url(string input)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(input));
        return Base64UrlEncode(hash);
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private sealed class TokenEndpointResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; init; }

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; init; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; init; }
    }
}
