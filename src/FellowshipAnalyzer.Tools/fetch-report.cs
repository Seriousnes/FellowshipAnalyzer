#:package Microsoft.Extensions.Configuration.UserSecrets@10.0.0
#:property PublishAot=false

using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;

if (args.Length < 3)
{
    Console.Error.WriteLine("Usage: dotnet run src/FellowshipAnalyzer.Tools/fetch-report.cs <code> <fightId> <sourceId> [outputPath]");
    return 1;
}

var code = args[0];
var fightId = int.Parse(args[1]);
var sourceId = int.Parse(args[2]);

var repoRoot = FindRepoRoot();
var fileStem = string.Concat($"{code}-f{fightId}-s{sourceId}".Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '-' : c));
var outputPath = args.Length >= 4
    ? Path.GetFullPath(args[3])
    : Path.Combine(repoRoot, "raw-reports", $"{fileStem}.json");
Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

var userSecretId = LoadUserSecretId(repoRoot);
if (userSecretId is null)
    return 1;

var configuration = new ConfigurationBuilder().AddUserSecrets(userSecretId).Build();
var clientId = configuration["FellowshipLogs:ClientId"] ?? configuration["ClientId"];
var clientSecret = configuration["FellowshipLogs:ClientSecret"] ?? configuration["ClientSecret"];
if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
{
    Console.Error.WriteLine("FellowshipLogs credentials not found in user secrets store '" + userSecretId + "'.");
    return 1;
}

const string tokenEndpoint = "https://www.fellowshiplogs.com/oauth/token";
const string graphQlEndpoint = "https://www.fellowshiplogs.com/api/v2/client";

const string masterDataQuery = """
    query($code: String!) {
      reportData { report(code: $code) {
        title startTime endTime
        fights {
          id name encounterID kill startTime endTime difficulty friendlyPlayers inProgress fightPercentage
          enemyNPCs { id gameID instanceCount groupCount petOwner }
          dungeonPulls {
            id encounterID kill startTime endTime name
            enemyNPCs { id gameID minimumInstanceID maximumInstanceID minimumInstanceGroupID maximumInstanceGroupID }
          }
        }
        masterData {
          abilities { gameID name icon type }
          actors { id name type subType server icon }
        }
      } }
    }
    """;

const string eventsQuery = """
    query($code: String!, $fightIDs: [Int], $sourceID: Int!, $startTime: Float!) {
      reportData { report(code: $code) {
        events(fightIDs: $fightIDs, sourceID: $sourceID, startTime: $startTime, includeResources: true, limit: 10000) {
          data
          nextPageTimestamp
        }
      } }
    }
    """;

using var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };

Console.WriteLine("Authenticating with FellowshipLogs API...");
var token = await GetAccessTokenAsync(httpClient, tokenEndpoint, clientId, clientSecret);

Console.WriteLine($"Fetching master data for report {code}...");
var masterDoc = await PostGraphQlAsync(httpClient, graphQlEndpoint, token, masterDataQuery, new JsonObject { ["code"] = code });
var report = masterDoc["data"]?["reportData"]?["report"]
    ?? throw new InvalidOperationException("Report not found (check the code and that the log is accessible).");

var fights = report["fights"]?.AsArray() ?? throw new InvalidOperationException("No fights on report.");
var fight = fights.FirstOrDefault(f => (int?)f?["id"] == fightId)
    ?? throw new InvalidOperationException($"Fight {fightId} not found on report {code}.");
var fightName = (string?)fight["name"] ?? "(unnamed)";
var pulls = fight["dungeonPulls"]?.AsArray();
Console.WriteLine($"  Fight {fightId} \"{fightName}\": {pulls?.Count ?? 0} dungeon pulls, encounterID={fight["encounterID"]}, kill={fight["kill"]}.");

var events = new JsonArray();
double startTime = 0;
var page = 0;
while (true)
{
    page++;
    Console.Write($"  Events page {page} (startTime={startTime:F0})...");
    var variables = new JsonObject
    {
        ["code"] = code,
        ["fightIDs"] = new JsonArray(fightId),
        ["sourceID"] = sourceId,
        ["startTime"] = startTime,
    };
    var eventsDoc = await PostGraphQlAsync(httpClient, graphQlEndpoint, token, eventsQuery, variables);
    var paginator = eventsDoc["data"]?["reportData"]?["report"]?["events"];
    var pageData = paginator?["data"]?.AsArray();
    var added = 0;
    if (pageData is not null)
    {
        foreach (var e in pageData.ToArray())
        {
            e!.Parent!.AsArray().Remove(e);
            events.Add(e);
            added++;
        }
    }
    var next = (double?)paginator?["nextPageTimestamp"];
    Console.WriteLine($" {added} events (total {events.Count}).");
    if (next is null or 0)
        break;
    startTime = next.Value;
}

var output = new JsonObject
{
    ["data"] = new JsonObject
    {
        ["reportData"] = new JsonObject
        {
            ["report"] = new JsonObject
            {
                ["code"] = code,
                ["title"] = report["title"]?.DeepClone(),
                ["startTime"] = report["startTime"]?.DeepClone(),
                ["endTime"] = report["endTime"]?.DeepClone(),
                ["events"] = new JsonObject { ["data"] = events },
                ["fights"] = new JsonArray(fight.DeepClone()),
                ["masterData"] = report["masterData"]?.DeepClone(),
            },
        },
    },
};

await using (var stream = File.Create(outputPath))
    await JsonSerializer.SerializeAsync(stream, output, new JsonSerializerOptions { WriteIndented = false });

var sizeMb = new FileInfo(outputPath).Length / 1024.0 / 1024.0;
Console.WriteLine($"Saved {events.Count} events ({sizeMb:F1} MB) to {outputPath}");
return 0;

static async Task<JsonObject> PostGraphQlAsync(HttpClient http, string endpoint, string token, string query, JsonObject variables)
{
    using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    var body = new JsonObject { ["query"] = query, ["variables"] = variables };
    request.Content = new StringContent(body.ToJsonString(), System.Text.Encoding.UTF8, "application/json");
    using var response = await http.SendAsync(request);
    var text = await response.Content.ReadAsStringAsync();
    if (!response.IsSuccessStatusCode)
        throw new InvalidOperationException($"GraphQL request failed ({(int)response.StatusCode}): {text}");
    var node = JsonNode.Parse(text)!.AsObject();
    if (node["errors"] is JsonArray errors && errors.Count > 0)
        throw new InvalidOperationException("GraphQL errors: " + errors.ToJsonString());
    return node;
}

static async Task<string> GetAccessTokenAsync(HttpClient httpClient, string tokenEndpoint, string clientId, string clientSecret)
{
    using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint)
    {
        Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
        }),
    };
    using var tokenResponse = await httpClient.SendAsync(tokenRequest);
    tokenResponse.EnsureSuccessStatusCode();
    using var payload = await JsonDocument.ParseAsync(await tokenResponse.Content.ReadAsStreamAsync());
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
    throw new InvalidOperationException("Could not find repository root (no .slnx file found).");
}

static string? LoadUserSecretId(string repoRoot)
{
    var envPath = Path.Combine(repoRoot, ".env");
    if (!File.Exists(envPath))
    {
        Console.Error.WriteLine($"No .env file found at {envPath}. Add USER_SECRET_ID=<id>.");
        return null;
    }
    foreach (var raw in File.ReadAllLines(envPath))
    {
        var line = raw.Trim();
        if (line.Length == 0 || line.StartsWith('#')) continue;
        var sep = line.IndexOf('=');
        if (sep <= 0) continue;
        if (!line[..sep].Trim().Equals("USER_SECRET_ID", StringComparison.OrdinalIgnoreCase)) continue;
        var value = line[(sep + 1)..].Trim().Trim('"');
        if (value.Length > 0) return value;
    }
    Console.Error.WriteLine($"USER_SECRET_ID not set in {envPath}.");
    return null;
}
