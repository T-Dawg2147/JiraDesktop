using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using JiraDashboardApp.Core.Interfaces;
using JiraDashboardApp.Core.Models;
using JiraDashboardApp.Core.Options;
using Microsoft.Extensions.Options;

namespace JiraDashboardApp.Infrastructure.Services;

public class JiraOAuthService : IJiraOAuthService
{
    private readonly HttpClient _http;
    private readonly JiraOAuthOptions _options;
    private readonly SemaphoreSlim _authLock = new(1, 1);

    private static readonly string StoreDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JiraDashboardApp");
    private static readonly string TokenFile = Path.Combine(StoreDir, "oauth-token.json");
    private static readonly string CloudFile = Path.Combine(StoreDir, "oauth-cloudid.txt");

    public JiraOAuthService(HttpClient http, IOptions<JiraOAuthOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    public async Task EnsureLoggedInAsync(CancellationToken ct = default)
    {
        await _authLock.WaitAsync(ct);
        try
        {
            var token = await LoadTokenAsync(ct);

            if (token is not null &&
                !string.IsNullOrWhiteSpace(token.AccessToken) &&
                token.ExpiresAt > DateTime.UtcNow.AddMinutes(2))
            {
                return;
            }

            if (token is not null && !string.IsNullOrWhiteSpace(token.RefreshToken))
            {
                try
                {
                    var refreshed = await RefreshTokenAsync(token.RefreshToken, ct);
                    await SaveTokenAsync(refreshed, ct);
                    return;
                }
                catch
                {
                    // fall through to interactive
                }
            }

            var newToken = await RunInteractiveLoginAsync(ct);
            await SaveTokenAsync(newToken, ct);
        }
        finally
        {
            _authLock.Release();
        }
    }

    public async Task<string> GetValidAccessTokenAsync(CancellationToken ct = default)
    {
        await EnsureLoggedInAsync(ct);
        var token = await LoadTokenAsync(ct) ?? throw new InvalidOperationException("OAuth token missing.");
        return token.AccessToken;
    }

    public async Task<string> GetCloudIdAsync(CancellationToken ct = default)
    {
        if (File.Exists(CloudFile))
        {
            var existing = (await File.ReadAllTextAsync(CloudFile, ct)).Trim();
            if (!string.IsNullOrWhiteSpace(existing))
            {
                Console.WriteLine($"Using cached cloudId: {existing}");
                return existing;
            }
        }

        // This is the reason the login screen is running twice, the UI button runs "EnsureLoggedInAsync"
        // and once it gets to "GetCloudIdAsync", its runs this method which its trying to "EnsureLoggedIn" again
        var accessToken = await GetValidAccessTokenAsync(ct);

        using var req = new HttpRequestMessage(HttpMethod.Get, "https://api.atlassian.com/oauth/token/accessible-resources");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var resp = await _http.SendAsync(req, ct);
        var json = await resp.Content.ReadAsStringAsync(ct);

        Console.WriteLine($"accessible-resources status: {(int)resp.StatusCode}");
        Console.WriteLine($"accessible-resources body: {json}");

        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"accessible-resources failed {(int)resp.StatusCode}: {json}");

        var resources = JsonSerializer.Deserialize<List<AccessibleResourceDto>>(json, JsonOpts()) ?? new();

        var chosen = resources.FirstOrDefault(r =>
                         !string.IsNullOrWhiteSpace(_options.SiteUrl) &&
                         string.Equals(r.Url.TrimEnd('/'), _options.SiteUrl!.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
                     ?? resources.FirstOrDefault();

        if (chosen is null)
            throw new InvalidOperationException("No accessible Jira resources returned for this account/app.");

        Directory.CreateDirectory(StoreDir);
        await File.WriteAllTextAsync(CloudFile, chosen.Id, ct);

        Console.WriteLine($"Saved cloudId: {chosen.Id} ({chosen.Url})");
        return chosen.Id;
    }

    private async Task<JiraOAuthTokenResponse> RunInteractiveLoginAsync(CancellationToken ct)
    {
        var redirect = new Uri(_options.RedirectUri); // e.g. http://localhost:51234/callback
        var expectedPath = redirect.AbsolutePath;     // /callback
        var prefix = $"{redirect.Scheme}://{redirect.Host}:{redirect.Port}/";

        // URL-safe state without '+', '/' or '=' to avoid decode ambiguity
        var state = CreateUrlSafeState();

        using var listener = new HttpListener();
        listener.Prefixes.Add(prefix);
        listener.Start();

        var authUrl = BuildAuthorizeUrl(state);

        Process.Start(new ProcessStartInfo
        {
            FileName = authUrl,
            UseShellExecute = true
        });

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromMinutes(3));

        while (true)
        {
            var getContextTask = listener.GetContextAsync();
            var completed = await Task.WhenAny(getContextTask, Task.Delay(Timeout.Infinite, timeoutCts.Token));

            if (completed != getContextTask)
                throw new TimeoutException("Timed out waiting for OAuth callback.");

            var context = await getContextTask;
            var req = context.Request;

            if (!string.Equals(req.Url?.AbsolutePath, expectedPath, StringComparison.OrdinalIgnoreCase))
            {
                await WriteTextResponse(context.Response, 404, "Not found", ct);
                continue;
            }

            var code = req.QueryString["code"];
            var returnedState = req.QueryString["state"];
            var error = req.QueryString["error"];

            if (!string.IsNullOrWhiteSpace(error))
            {
                await WriteTextResponse(context.Response, 400, $"OAuth error: {error}", ct);
                throw new InvalidOperationException($"OAuth provider returned error: {error}");
            }

            // normalize both states in case provider/url parsing transforms chars
            var normExpected = NormalizeState(state);
            var normReturned = NormalizeState(returnedState);

            if (!string.Equals(normExpected, normReturned, StringComparison.Ordinal))
            {
                await WriteTextResponse(context.Response, 400, "State mismatch. Please return to the app and try again.", ct);
                continue;
            }

            if (string.IsNullOrWhiteSpace(code))
            {
                await WriteTextResponse(context.Response, 400, "Missing code.", ct);
                continue;
            }

            await WriteHtmlResponse(context.Response, 200, "<h2>Login successful. You can close this tab.</h2>", ct);

            var token = await ExchangeCodeForTokenAsync(code, ct);
            return token;
        }
    }

    private string BuildAuthorizeUrl(string state)
    {
        var query = new Dictionary<string, string>
        {
            ["audience"] = _options.Audience,
            ["client_id"] = _options.ClientId,
            ["scope"] = _options.Scopes,
            ["redirect_uri"] = _options.RedirectUri,
            ["state"] = state,
            ["response_type"] = "code",
            ["prompt"] = "consent"
        };

        var q = string.Join("&", query.Select(kvp =>
            $"{WebUtility.UrlEncode(kvp.Key)}={WebUtility.UrlEncode(kvp.Value)}"));

        return $"{_options.AuthUrl}?{q}";
    }

    private async Task<JiraOAuthTokenResponse> ExchangeCodeForTokenAsync(string code, CancellationToken ct)
    {
        var payload = new
        {
            grant_type = "authorization_code",
            client_id = _options.ClientId,
            client_secret = _options.ClientSecret,
            code,
            redirect_uri = _options.RedirectUri
        };

        using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var resp = await _http.PostAsync(_options.TokenUrl, content, ct);
        var json = await resp.Content.ReadAsStringAsync(ct);
        resp.EnsureSuccessStatusCode();

        var dto = JsonSerializer.Deserialize<TokenDto>(json, JsonOpts())
                  ?? throw new InvalidOperationException("Failed parsing token response.");

        return new JiraOAuthTokenResponse
        {
            AccessToken = dto.AccessToken,
            RefreshToken = dto.RefreshToken ?? string.Empty,
            ExpiresIn = dto.ExpiresIn,
            ExpiresAt = DateTime.UtcNow.AddSeconds(dto.ExpiresIn)
        };
    }

    private async Task<JiraOAuthTokenResponse> RefreshTokenAsync(string refreshToken, CancellationToken ct)
    {
        var payload = new
        {
            grant_type = "refresh_token",
            client_id = _options.ClientId,
            client_secret = _options.ClientSecret,
            refresh_token = refreshToken
        };

        using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var resp = await _http.PostAsync(_options.TokenUrl, content, ct);
        var json = await resp.Content.ReadAsStringAsync(ct);
        resp.EnsureSuccessStatusCode();

        var dto = JsonSerializer.Deserialize<TokenDto>(json, JsonOpts())
                  ?? throw new InvalidOperationException("Failed parsing refresh response.");

        return new JiraOAuthTokenResponse
        {
            AccessToken = dto.AccessToken,
            RefreshToken = dto.RefreshToken ?? refreshToken,
            ExpiresIn = dto.ExpiresIn,
            ExpiresAt = DateTime.UtcNow.AddSeconds(dto.ExpiresIn)
        };
    }

    private static string CreateUrlSafeState()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static string NormalizeState(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return string.Empty;
        // undo common URL encoding variance
        return Uri.UnescapeDataString(s).Trim();
    }

    private static async Task<JiraOAuthTokenResponse?> LoadTokenAsync(CancellationToken ct)
    {
        if (!File.Exists(TokenFile)) return null;
        await using var s = File.OpenRead(TokenFile);
        return await JsonSerializer.DeserializeAsync<JiraOAuthTokenResponse>(s, cancellationToken: ct);
    }

    private static async Task SaveTokenAsync(JiraOAuthTokenResponse token, CancellationToken ct)
    {
        Directory.CreateDirectory(StoreDir);
        await using var s = File.Create(TokenFile);
        await JsonSerializer.SerializeAsync(s, token, cancellationToken: ct);
    }

    private static async Task WriteTextResponse(HttpListenerResponse response, int status, string body, CancellationToken ct)
    {
        var bytes = Encoding.UTF8.GetBytes(body);
        response.StatusCode = status;
        response.ContentType = "text/plain";
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes, 0, bytes.Length, ct);
        response.OutputStream.Close();
    }

    private static async Task WriteHtmlResponse(HttpListenerResponse response, int status, string body, CancellationToken ct)
    {
        var html = $"<html><body>{body}</body></html>";
        var bytes = Encoding.UTF8.GetBytes(html);
        response.StatusCode = status;
        response.ContentType = "text/html";
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes, 0, bytes.Length, ct);
        response.OutputStream.Close();
    }

    private static JsonSerializerOptions JsonOpts() => new() { PropertyNameCaseInsensitive = true };

    private sealed class TokenDto
    {
        [System.Text.Json.Serialization.JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("token_type")]
        public string? TokenType { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("scope")]
        public string? Scope { get; set; }
    }

    private sealed class AccessibleResourceDto
    {
        public string Id { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }
}