using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;

namespace FellowshipAnalyzer.FellowshipLogs;

/// <summary>
/// Zero-deserialization streaming proxy: authenticates with Fellowship Logs and pipes
/// raw gzip-compressed GraphQL responses directly to the browser.
/// </summary>
public sealed class FellowshipLogsProxy(IHttpClientFactory httpClientFactory, FellowshipLogsClientOptions options)
{
    private const string ReportQuery = """
        query GetReport($code: String!) {
          reportData {
            report(code: $code) {
              title
              startTime
              endTime
              fights {
                id
                name
                encounterID
                kill
                startTime
                endTime
                difficulty
                friendlyPlayers
                inProgress
              }
              masterData {
                actors {
                  id
                  name
                  type
                  subType
                  server
                }
              }
            }
          }
        }
        """;

    private const string EventsQuery = """
        query ReportEvents($code: String!, $fightIDs: [Int!], $sourceID: Int!) {
          reportData {
            report(code: $code) {
              fights(fightIDs: $fightIDs) {
                inProgress
              }
              events(fightIDs: $fightIDs, sourceID: $sourceID, useAbilityIDs: true) {
                data
                nextPageTimestamp
              }
            }
          }
        }
        """;

    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private string? _cachedToken;
    private DateTimeOffset _tokenExpiresAt;

    public async Task<HttpResponseMessage> ProxyReportAsync(string reportCode, CancellationToken cancellationToken)
    {
        var payload = new
        {
            query = ReportQuery,
            variables = new { code = reportCode }
        };
        return await ProxyRequestAsync(payload, cancellationToken);
    }

    public async Task<HttpResponseMessage> ProxyEventsAsync(
        string reportCode,
        int playerId,
        int fightId,
        CancellationToken cancellationToken)
    {
        var payload = new
        {
            query = EventsQuery,
            variables = new
            {
                code = reportCode,
                fightIDs = new[] { fightId },
                sourceID = playerId
            }
        };
        return await ProxyRequestAsync(payload, cancellationToken);
    }

    private async Task<HttpResponseMessage> ProxyRequestAsync(object payload, CancellationToken cancellationToken)
    {
        var token = await GetTokenAsync(cancellationToken);
        var client = httpClientFactory.CreateClient("FellowshipLogsProxy");

        var request = new HttpRequestMessage(HttpMethod.Post, options.GraphQlEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
        request.Content = JsonContent.Create(payload);

        return await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
    }

    private async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(_cachedToken) && DateTimeOffset.UtcNow < _tokenExpiresAt)
            return _cachedToken;

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            if (!string.IsNullOrEmpty(_cachedToken) && DateTimeOffset.UtcNow < _tokenExpiresAt)
                return _cachedToken;

            if (string.IsNullOrWhiteSpace(options.ClientId) || string.IsNullOrWhiteSpace(options.ClientSecret))
            {
                throw new InvalidOperationException(
                    "FellowshipLogs credentials are missing. Configure FellowshipLogs:ClientId and FellowshipLogs:ClientSecret.");
            }

            using var client = httpClientFactory.CreateClient("FellowshipLogsProxy");
            using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, options.TokenEndpoint)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = options.ClientId,
                    ["client_secret"] = options.ClientSecret
                })
            };

            using var tokenResponse = await client.SendAsync(tokenRequest, cancellationToken);
            tokenResponse.EnsureSuccessStatusCode();

            var oauthResponse = await tokenResponse.Content.ReadFromJsonAsync<OAuthTokenResponse>(cancellationToken)
                ?? throw new InvalidOperationException("Token endpoint returned null.");

            if (string.IsNullOrWhiteSpace(oauthResponse.AccessToken))
                throw new InvalidOperationException("Token endpoint did not return an access token.");

            _cachedToken = oauthResponse.AccessToken;
            _tokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(Math.Max(60, oauthResponse.ExpiresIn - 60));
            return _cachedToken;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private sealed class OAuthTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = "";

        [JsonPropertyName("expires_in")]
        public long ExpiresIn { get; set; }
    }
}
