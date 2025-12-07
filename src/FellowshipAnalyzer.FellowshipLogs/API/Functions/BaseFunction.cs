using System.Net.Http.Headers;
using System.Text.Json.Serialization;

namespace FellowshipAnalyzer.FellowshipLogs.API.Functions;

internal abstract class BaseFunction(IApiRequestExecutor api, FellowshipLogsClientOptions options)
{
    private readonly ClientCredentialsTokenCache _tokenCache = new(api, options);

    protected async Task<TResult> ApiRequestAsync<TResult>(object query, CancellationToken cancellationToken)
    {
        var token = await _tokenCache.GetTokenAsync(cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Post, options.GraphQlEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.AddHeaders();
        request.Content = api.SerializeContent(query);
        return await api.ExecuteAsync<TResult>(request, cancellationToken);
    }
}

internal sealed class ClientCredentialsTokenCache(IApiRequestExecutor api, FellowshipLogsClientOptions options)
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

            var tokenResponse = await api.ExecuteAsync<OAuthTokenResponse>(tokenRequest, cancellationToken);

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

internal static class FunctionExtensions
{
    internal static HttpRequestMessage AddHeaders(this HttpRequestMessage request)
    {
        //request.Headers.Authorization = new("Bearer", Config.v2.Token.AccessToken);
        request.Headers.AcceptEncoding.Add(new("gzip"));
        return request;
    }
}