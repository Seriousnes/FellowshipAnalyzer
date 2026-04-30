#:package Microsoft.Extensions.Configuration.UserSecrets@10.0.0
#:property UserSecretsId=YOUR-USER-SECRETS-ID-HERE
#:property PublishAot=false

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;

bool refresh = args.Contains("--refresh", StringComparer.OrdinalIgnoreCase);

var repoRoot = FindRepoRoot();
var outputPath = Path.Combine(repoRoot, "abilities.json");

if (!refresh && File.Exists(outputPath))
{
    Console.WriteLine($"Using cached abilities from {outputPath}");
    Console.WriteLine("Pass --refresh to re-fetch from the API.");
    return 0;
}

var configuration = new ConfigurationBuilder()
    // Replace YOUR-USER-SECRETS-ID-HERE with your own UserSecretsId.
    // Run `dotnet user-secrets init` in the FellowshipAnalyzer project to generate one,
    // then set FellowshipLogs:ClientId and FellowshipLogs:ClientSecret via `dotnet user-secrets set`.
    .AddUserSecrets("fellowshipanalyzer-devapi")
    .Build();

var clientId = configuration["FellowshipLogs:ClientId"] ?? configuration["ClientId"];
var clientSecret = configuration["FellowshipLogs:ClientSecret"] ?? configuration["ClientSecret"];

if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
{
    Console.Error.WriteLine(
        "FellowshipLogs credentials not found. " +
        "Set FellowshipLogs:ClientId and FellowshipLogs:ClientSecret in user secrets.");
    return 1;
}

const string tokenEndpoint = "https://www.fellowshiplogs.com/oauth/token";
const string graphQlEndpoint = "https://www.fellowshiplogs.com/api/v2/client";

const string abilitiesQuery = """
    query abilities($page: Int) {
        gameData {
            abilities(page: $page) {
                data {
                    id
                    name
                    icon
                }
                current_page
                has_more_pages
                last_page
                total
            }
        }
    }
    """;

using var httpClient = new HttpClient();

Console.WriteLine("Authenticating with FellowshipLogs API...");
var token = await GetAccessTokenAsync(httpClient, tokenEndpoint, clientId, clientSecret);

var allAbilities = new List<AbilityEntry>();
var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
int page = 1;
bool hasMore = true;

while (hasMore)
{
    Console.Write($"  Fetching page {page}...");

    using var request = new HttpRequestMessage(HttpMethod.Post, graphQlEndpoint);
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    request.Content = JsonContent.Create(new { query = abilitiesQuery, variables = new { page } });

    using var response = await httpClient.SendAsync(request);
    response.EnsureSuccessStatusCode();

    var result = await response.Content.ReadFromJsonAsync<GraphQlResponse>(jsonOptions);
    var abilities = result?.Data?.GameData?.Abilities
        ?? throw new InvalidOperationException($"Unexpected response on page {page}.");

    allAbilities.AddRange(abilities.Data);
    hasMore = abilities.HasMorePages;

    Console.WriteLine($" {abilities.Data.Count} abilities (page {abilities.CurrentPage}/{abilities.LastPage}, total: {abilities.Total})");

    page++;
}

Console.WriteLine($"Fetched {allAbilities.Count} abilities total.");

var writeOptions = new JsonSerializerOptions { WriteIndented = true };
await using var stream = File.Create(outputPath);
await JsonSerializer.SerializeAsync(stream, allAbilities, writeOptions);

Console.WriteLine($"Saved to {outputPath}");
return 0;

// --- Helper methods ---

static async Task<string> GetAccessTokenAsync(
    HttpClient httpClient, string tokenEndpoint, string clientId, string clientSecret)
{
    using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint)
    {
        Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret
        })
    };

    using var tokenResponse = await httpClient.SendAsync(tokenRequest);
    tokenResponse.EnsureSuccessStatusCode();

    using var payload = await JsonDocument.ParseAsync(
        await tokenResponse.Content.ReadAsStreamAsync());

    return payload.RootElement.GetProperty("access_token").GetString()
        ?? throw new InvalidOperationException("Token endpoint did not return an access token.");
}

static string FindRepoRoot()
{
    foreach (var startDir in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
    {
        var dir = new DirectoryInfo(startDir);
        while (dir is not null)
        {
            if (dir.GetFiles("*.slnx").Length > 0)
                return dir.FullName;
            dir = dir.Parent;
        }
    }

    throw new InvalidOperationException(
        "Could not find repository root (no .slnx file found in parent directories).");
}

// --- Response models ---

record GraphQlResponse(GameDataWrapper Data);
record GameDataWrapper(GameDataContent GameData);
record GameDataContent(AbilitiesPagination Abilities);

record AbilitiesPagination(
    List<AbilityEntry> Data,
    [property: JsonPropertyName("current_page")] int CurrentPage,
    [property: JsonPropertyName("has_more_pages")] bool HasMorePages,
    [property: JsonPropertyName("last_page")] int LastPage,
    int Total);

record AbilityEntry(int Id, string Name, string? Icon);
