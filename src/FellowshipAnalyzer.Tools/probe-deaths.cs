#:package Microsoft.Extensions.Configuration.UserSecrets@10.0.0
#:property PublishAot=false

using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;

var scanMode = args.Contains("--scan");
var positional = args.Where(a => !a.StartsWith("--")).ToArray();
var code = positional.Length >= 1 ? positional[0] : "RaMDvgzWXBCnF4QT";
var dungeonId = positional.Length >= 2 ? int.Parse(positional[1]) : 16;
var playerId = positional.Length >= 3 ? int.Parse(positional[2]) : 25;

var repoRoot = FindRepoRoot();
var userSecretId = LoadUserSecretId(repoRoot);
if (userSecretId is null) return 1;

var configuration = new ConfigurationBuilder().AddUserSecrets(userSecretId).Build();
var clientId = configuration["FellowshipLogs:ClientId"] ?? configuration["ClientId"];
var clientSecret = configuration["FellowshipLogs:ClientSecret"] ?? configuration["ClientSecret"];
if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
{
    Console.Error.WriteLine("FellowshipLogs credentials not found in user secrets store '" + userSecretId + "'.");
    return 1;
}

const string tokenEndpoint = "https://www.fellowshiplogs.com/oauth/token";
const string clientEndpoint = "https://www.fellowshiplogs.com/api/v2/client";
const string userEndpoint = "https://www.fellowshiplogs.com/api/v2/user";

const string actorsQuery = """
    query($code: String!) {
      reportData { report(code: $code) {
        masterData { actors { id name type subType } }
      } }
    }
    """;

const string eventsQuery = """
    query($code: String!, $fightIDs: [Int], $sourceID: Int, $dataType: EventDataType, $hostilityType: HostilityType, $startTime: Float) {
      reportData { report(code: $code) {
        events(fightIDs: $fightIDs, sourceID: $sourceID, dataType: $dataType, hostilityType: $hostilityType, startTime: $startTime, includeResources: false, limit: 10000) {
          data
          nextPageTimestamp
        }
      } }
    }
    """;

using var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };

Console.WriteLine($"Authenticating (client_credentials)...");
var appToken = await GetAccessTokenAsync(httpClient, tokenEndpoint, clientId, clientSecret);
var userToken = configuration["FellowshipLogs:AccessToken"];

var candidates = new (string TokenName, string Token, string EndpointName, string Endpoint)[]
{
    ("app",  appToken,        "client", clientEndpoint),
    ("user", userToken ?? "", "client", clientEndpoint),
    ("user", userToken ?? "", "user",   userEndpoint),
    ("app",  appToken,        "user",   userEndpoint),
};

Console.WriteLine($"Resolving a token/endpoint that can see {code}...");
string token = "", graphQlEndpoint = "";
JsonObject? actorsDoc = null;
foreach (var c in candidates)
{
    if (string.IsNullOrWhiteSpace(c.Token)) continue;
    try
    {
        actorsDoc = await PostGraphQlAsync(httpClient, c.Endpoint, c.Token, actorsQuery, new JsonObject { ["code"] = code });
        token = c.Token; graphQlEndpoint = c.Endpoint;
        Console.WriteLine($"  OK: {c.TokenName} token @ /{c.EndpointName} endpoint CAN see this report.");
        break;
    }
    catch (InvalidOperationException ex)
    {
        Console.WriteLine($"  no: {c.TokenName} token @ /{c.EndpointName} -> {Trim(ex.Message)}");
    }
}
if (actorsDoc is null)
{
    Console.Error.WriteLine("No token/endpoint combination can see this report. It was likely deleted or made private.");
    return 1;
}

static string Trim(string s) => s.Length > 140 ? s[..140] + "..." : s;
var actorMap = new Dictionary<int, (string Name, string Type)>();
foreach (var a in actorsDoc["data"]?["reportData"]?["report"]?["masterData"]?["actors"]?.AsArray() ?? new JsonArray())
{
    var id = (int?)a?["id"];
    if (id is null) continue;
    actorMap[id.Value] = ((string?)a?["name"] ?? "?", (string?)a?["type"] ?? "?");
}
Console.WriteLine($"  {actorMap.Count} actors. Player {playerId} = {Label(playerId)}");

var variants = new (string Name, int? SourceId, string? Hostility)[]
{
    ("V1  source=player, hostility=default(Friendlies)", playerId, null),
    ("V2  source=ALL,    hostility=default(Friendlies)", 0, null),
    ("V3  source=ALL,    hostility=Friendlies",          0, "Friendlies"),
    ("V4  source=ALL,    hostility=Enemies",             0, "Enemies"),
    ("V5  source=player, hostility=Enemies",             playerId, "Enemies"),
};

foreach (var v in variants)
{
    Console.WriteLine();
    Console.WriteLine("================================================================");
    Console.WriteLine(v.Name);
    var deaths = await FetchDeathsAsync(httpClient, graphQlEndpoint, token, eventsQuery, code, dungeonId, v.SourceId, v.Hostility);
    Report(deaths);
}

Console.WriteLine();
Console.WriteLine("================================================================");
Console.WriteLine("COMBINED aliased query (exact GetDeaths.graphql shape)");
const string aliasedQuery = """
    query($code: String!, $fightIDs: [Int]) {
      reportData { report(code: $code) {
        fights(fightIDs: $fightIDs) { inProgress }
        enemyDeaths: events(fightIDs: $fightIDs, dataType: Deaths, hostilityType: Enemies) { data }
        friendlyDeaths: events(fightIDs: $fightIDs, dataType: Deaths, hostilityType: Friendlies) { data }
      } }
    }
    """;
var aliasedDoc = await PostGraphQlAsync(httpClient, graphQlEndpoint, token, aliasedQuery,
    new JsonObject { ["code"] = code, ["fightIDs"] = new JsonArray(dungeonId) });
var rep = aliasedDoc["data"]?["reportData"]?["report"];
var enemyCount = rep?["enemyDeaths"]?["data"]?.AsArray()?.Count ?? 0;
var friendlyCount = rep?["friendlyDeaths"]?["data"]?.AsArray()?.Count ?? 0;
var ip = (bool?)rep?["fights"]?.AsArray()?.FirstOrDefault()?["inProgress"] ?? false;
Console.WriteLine($"  inProgress={ip}  enemyDeaths={enemyCount}  friendlyDeaths={friendlyCount}  total={enemyCount + friendlyCount}");

Console.WriteLine();
Console.WriteLine("================================================================");
Console.WriteLine($"NAMESPACE ALIGNMENT: player {playerId} kills in player-stream vs Enemies-view");
var playerAll = await FetchEventsAsync(httpClient, graphQlEndpoint, token, eventsQuery, code, dungeonId, playerId, null, null);
var playerKills = playerAll.Where(e => (string?)e?["type"] == "death").ToList();
var enemyView = await FetchEventsAsync(httpClient, graphQlEndpoint, token, eventsQuery, code, dungeonId, 0, "Deaths", "Enemies");
var enemyBySource = enemyView.Where(e => (int?)e?["sourceID"] == playerId).ToList();

Console.WriteLine($"  player-stream death events (kills by {playerId}): {playerKills.Count}");
Console.WriteLine($"  Enemies-view deaths with source={playerId}:        {enemyBySource.Count}");

var strictP = new HashSet<string>(playerKills.Select(StrictKey));
var strictE = new HashSet<string>(enemyBySource.Select(StrictKey));
var looseP = new HashSet<string>(playerKills.Select(LooseKey));
var looseE = new HashSet<string>(enemyBySource.Select(LooseKey));
Console.WriteLine($"  strict (ts,target,instance) matched: {strictP.Intersect(strictE).Count()} | player-only {strictP.Except(strictE).Count()} | enemy-only {strictE.Except(strictP).Count()}");
Console.WriteLine($"  loose  (ts,target)          matched: {looseP.Intersect(looseE).Count()} | player-only {looseP.Except(looseE).Count()} | enemy-only {looseE.Except(looseP).Count()}");
Console.WriteLine("  sample player-stream kill: " + (playerKills.FirstOrDefault()?.ToJsonString() ?? "(none)"));
Console.WriteLine("  sample enemies-view death: " + (enemyBySource.FirstOrDefault()?.ToJsonString() ?? "(none)"));
var aligned = strictP.SetEquals(strictE) && strictP.Count > 0;
var alignedLoose = looseP.SetEquals(looseE) && looseP.Count > 0;
Console.WriteLine(aligned
    ? "  => NAMESPACES ALIGN exactly (ts,target,instance). Merge attribution is sound."
    : alignedLoose
        ? "  => targetID namespace aligns (ts,target); instance representation differs. Namespace sound."
        : "  => MISMATCH. Investigate actor-ID namespace before shipping.");

return 0;

static string StrictKey(JsonNode? e) => $"{(long?)e?["timestamp"]}/{(int?)e?["targetID"]}/{(int?)e?["targetInstance"]}";
static string LooseKey(JsonNode? e) => $"{(long?)e?["timestamp"]}/{(int?)e?["targetID"]}";

async Task<List<JsonNode>> FetchDeathsAsync(HttpClient http, string endpoint, string tok, string query,
    string reportCode, int dungeonId, int? sourceId, string? hostility)
    => await FetchEventsAsync(http, endpoint, tok, query, reportCode, dungeonId, sourceId, "Deaths", hostility);

async Task<List<JsonNode>> FetchEventsAsync(HttpClient http, string endpoint, string tok, string query,
    string reportCode, int dungeonId, int? sourceId, string? dataType, string? hostility)
{
    var all = new List<JsonNode>();
    double startTime = 0;
    while (true)
    {
        var variables = new JsonObject
        {
            ["code"] = reportCode,
            ["fightIDs"] = new JsonArray(dungeonId),
            ["sourceID"] = sourceId,
            ["startTime"] = startTime,
        };
        if (dataType is not null) variables["dataType"] = dataType;
        if (hostility is not null) variables["hostilityType"] = hostility;

        var doc = await PostGraphQlAsync(http, endpoint, tok, query, variables);
        var paginator = doc["data"]?["reportData"]?["report"]?["events"];
        var pageData = paginator?["data"]?.AsArray();
        if (pageData is not null)
            foreach (var e in pageData) if (e is not null) all.Add(e.DeepClone());
        var next = (double?)paginator?["nextPageTimestamp"];
        if (next is null or 0) break;
        startTime = next.Value;
    }
    return all;
}

void Report(List<JsonNode> events)
{
    var deaths = events.Where(e => (string?)e?["type"] == "death").ToList();
    Console.WriteLine($"  rows returned: {events.Count}   type==death: {deaths.Count}");
    if (deaths.Count == 0) { Console.WriteLine("  (no death rows)"); return; }

    var bySource = deaths.GroupBy(e => (int?)e?["sourceID"] ?? -1).OrderByDescending(g => g.Count());
    Console.WriteLine("  by SOURCE (killer):");
    foreach (var g in bySource) Console.WriteLine($"    {g.Count(),4} x  source {g.Key,4}  {Label(g.Key)}");

    var byTarget = deaths.GroupBy(e => (int?)e?["targetID"] ?? -1).OrderByDescending(g => g.Count());
    Console.WriteLine("  by TARGET (deceased):");
    foreach (var g in byTarget.Take(12)) Console.WriteLine($"    {g.Count(),4} x  target {g.Key,4}  {Label(g.Key)}");
    if (byTarget.Count() > 12) Console.WriteLine($"    ... {byTarget.Count() - 12} more target ids");

    var first = deaths[0]!.AsObject();
    Console.WriteLine("  first death event keys: " + string.Join(", ", first.Select(kv => kv.Key)));
    Console.WriteLine("  first death event: " + first.ToJsonString());
}

string Label(int id) => actorMap.TryGetValue(id, out var a) ? $"{a.Type}:{a.Name}" : "(unknown)";

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
            if (dir.GetFiles("*.slnx").Length > 0) return dir.FullName;
            dir = dir.Parent;
        }
    }
    throw new InvalidOperationException("Could not find repository root (no .slnx file found).");
}

static string? LoadUserSecretId(string repoRoot)
{
    var envPath = Path.Combine(repoRoot, ".env");
    if (!File.Exists(envPath)) { Console.Error.WriteLine($"No .env file found at {envPath}."); return null; }
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
