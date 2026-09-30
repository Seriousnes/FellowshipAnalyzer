using System.Net;
using System.Text.Json;

using Shouldly;

using Xunit;

namespace FellowshipAnalyzer.Api.Tests;

public sealed class FunctionsHostTests(FunctionsHost host) : IClassFixture<FunctionsHost>
{
    /// <summary>
    /// One request per published function that the handler rejects before it reads storage or calls
    /// Fellowship Logs, so the response proves the handler ran without either being reachable.
    /// </summary>
    public static TheoryData<string, string, string> Probes => new()
    {
        { "GetAnalysis", "api/analysis/%20", "reportCode" },
        { "GetCharacterReports", "api/character/0", "id" },
        { "GetDeaths", "api/deaths", "reportCode" },
        { "GetEvents", "api/events", "reportCode" },
    };

    [Theory]
    [MemberData(nameof(Probes))]
    public async Task Function_RunsItsHandler_WhenTheHostInvokesIt(string function, string path, string parameter)
    {
        using var response = await host.Client.GetAsync(path);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(
            HttpStatusCode.BadRequest,
            $"{function} answered {path} with {(int)response.StatusCode} and a {body.Length}-character body instead of its handler's 400.{Environment.NewLine}{host.Output}");
        body.ShouldContain($"'{parameter}'", customMessage: $"{function} body: {body}");
    }

    [Fact]
    public void EveryPublishedFunction_HasAProbe()
    {
        using var metadata = JsonDocument.Parse(File.ReadAllText(Path.Combine(host.PublishDirectory, "functions.metadata")));
        var published = metadata.RootElement.EnumerateArray()
            .Select(function => function.GetProperty("name").GetString())
            .ToList();

        published.ShouldBe(Probes.Select(row => (string?)row[0]), ignoreOrder: true);
    }
}
