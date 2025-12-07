using System.Text.Json;
using System.Text.Json.Serialization;

namespace FellowshipAnalyzer.FellowshipLogs.API;

internal sealed class ClientCredentialsTokenCache(HttpClient httpClient, FellowshipLogsClientOptions options)
{
    private sealed class OAuthTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = "";

        [JsonPropertyName("expires_in")]
        public long ExpiresIn { get; set; }
    }

    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private string? _token;
    private DateTimeOffset _expiresAt;

    public async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.ClientId) || string.IsNullOrWhiteSpace(options.ClientSecret))
        {
            throw new InvalidOperationException(
                "FellowshipLogs credentials are missing. Configure FellowshipLogs:ClientId and FellowshipLogs:ClientSecret (or top-level ClientId/ClientSecret).");
        }

        if (!string.IsNullOrWhiteSpace(_token) && DateTimeOffset.UtcNow < _expiresAt)
        {
            return _token;
        }

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            if (!string.IsNullOrWhiteSpace(_token) && DateTimeOffset.UtcNow < _expiresAt)
            {
                return _token;
            }

            using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, options.TokenEndpoint)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = options.ClientId,
                    ["client_secret"] = options.ClientSecret
                })
            };

            using var response = await httpClient.SendAsync(tokenRequest, cancellationToken);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var tokenResponse = await JsonSerializer.DeserializeAsync<OAuthTokenResponse>(stream, cancellationToken: cancellationToken)
                ?? throw new InvalidOperationException("Token endpoint did not return a response.");

            _token = tokenResponse.AccessToken;
            _expiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(60, tokenResponse.ExpiresIn - 60));

            if (string.IsNullOrWhiteSpace(_token))
            {
                throw new InvalidOperationException("Token endpoint did not return an access token.");
            }

            return _token;
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
