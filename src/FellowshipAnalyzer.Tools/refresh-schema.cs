#:package HotChocolate.Utilities.Introspection@16.0.0-rc.1.40
#:package Microsoft.Extensions.Configuration.UserSecrets@10.0.0
#:property JsonSerializerIsReflectionEnabledByDefault=true
#:property PublishAot=false
#:property WarningLevel=0

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using HotChocolate.Utilities.Introspection;

using Microsoft.Extensions.Configuration;

var repoRoot = FindRepoRoot();
var outputPath = Path.Combine(
    repoRoot,
    "src", "FellowshipAnalyzer", "FellowshipAnalyzer.Api.GraphQL", "schema.graphql");

{
    var userSecretId = LoadUserSecretId(repoRoot);
    if (userSecretId is null)
        return 1;

    var configuration = new ConfigurationBuilder()
        .AddUserSecrets(userSecretId)
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

    const string introspectionQuery = """
        {
          __schema {
            queryType { name }
            mutationType { name }
            subscriptionType { name }
            types {
              ...FullType
            }
            directives {
              name
              description
              locations
              args {
                ...InputValue
              }
            }
          }
        }
        fragment FullType on __Type {
          kind
          name
          description
          fields(includeDeprecated: true) {
            name
            description
            args {
              ...InputValue
            }
            type {
              ...TypeRef
            }
            isDeprecated
            deprecationReason
          }
          inputFields {
            ...InputValue
          }
          interfaces {
            ...TypeRef
          }
          enumValues(includeDeprecated: true) {
            name
            description
            isDeprecated
            deprecationReason
          }
          possibleTypes {
            ...TypeRef
          }
        }
        fragment InputValue on __InputValue {
          name
          description
          type { ...TypeRef }
          defaultValue
        }
        fragment TypeRef on __Type {
          kind
          name
          ofType {
            kind
            name
            ofType {
              kind
              name
              ofType {
                kind
                name
                ofType {
                  kind
                  name
                  ofType {
                    kind
                    name
                    ofType {
                      kind
                      name
                      ofType {
                        kind
                        name
                      }
                    }
                  }
                }
              }
            }
          }
        }
        """;

    using var httpClient = new HttpClient();

    Console.WriteLine("Authenticating with FellowshipLogs API...");
    var token = await GetAccessTokenAsync(httpClient, tokenEndpoint, clientId, clientSecret);

    Console.WriteLine("Fetching introspection schema...");
    using var request = new HttpRequestMessage(HttpMethod.Post, graphQlEndpoint);
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    request.Content = JsonContent.Create(new { query = introspectionQuery });

    using var response = await httpClient.SendAsync(request);
    response.EnsureSuccessStatusCode();

    var responseJson = await response.Content.ReadAsStringAsync();

    Console.WriteLine("Converting introspection JSON to SDL...");
    using var mockClient = new HttpClient(new FixedResponseHandler(responseJson))
    {
        BaseAddress = new Uri("https://localhost/graphql")
    };
    var document = await IntrospectionClient.IntrospectServerAsync(mockClient);
    var sdl = document.ToString(indented: true);

    Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
    await File.WriteAllTextAsync(outputPath, sdl, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    Console.WriteLine($"Schema SDL written to {outputPath}");
}
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

static string? LoadUserSecretId(string repoRoot)
{
    var envPath = Path.Combine(repoRoot, ".env.local");
    if (!File.Exists(envPath))
    {
        Console.Error.WriteLine(
            $"No .env.local file found at {envPath}. " +
            "Create one with: USER_SECRET_ID=<your-user-secrets-id>");
        return null;
    }

    foreach (var raw in File.ReadAllLines(envPath))
    {
        var line = raw.Trim();
        if (line.Length == 0 || line.StartsWith('#'))
            continue;

        var separator = line.IndexOf('=');
        if (separator <= 0)
            continue;

        if (!line[..separator].Trim().Equals("USER_SECRET_ID", StringComparison.OrdinalIgnoreCase))
            continue;

        var value = line[(separator + 1)..].Trim().Trim('"');
        if (value.Length > 0)
            return value;
    }

    Console.Error.WriteLine(
        $"USER_SECRET_ID not set in {envPath}. Add a line: USER_SECRET_ID=<your-user-secrets-id>");
    return null;
}

// --- Handler ---

sealed class FixedResponseHandler(string jsonContent) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(jsonContent, Encoding.UTF8, "application/json")
        };
        return Task.FromResult(response);
    }
}


