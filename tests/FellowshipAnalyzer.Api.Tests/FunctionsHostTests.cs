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

    /// <summary>Functions with a probe of their own below rather than a row of <see cref="Probes"/>.</summary>
    private static readonly string[] InsightsFunctions = ["GetInsightsAvailability", "GetGuideInsights"];

    [Fact]
    public async Task GetInsightsAvailability_AnswersOff_WithoutTheSetting()
    {
        using var response = await host.Client.GetAsync("api/insights");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(
            HttpStatusCode.OK,
            $"GetInsightsAvailability answered with {(int)response.StatusCode}: {body}{Environment.NewLine}{host.Output}");
        JsonDocument.Parse(body).RootElement.GetProperty("available").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task GetGuideInsights_AnswersNotAvailable_WithoutTheSetting()
    {
        using var response = await host.Client.PostAsync("api/insights/aeona/oblivion", new StringContent("{}"));
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(
            HttpStatusCode.ServiceUnavailable,
            $"GetGuideInsights answered with {(int)response.StatusCode}: {body}{Environment.NewLine}{host.Output}");
        body.ShouldContain("not available");
    }

    [Fact]
    public void EveryPublishedFunction_HasAProbe()
    {
        using var metadata = JsonDocument.Parse(File.ReadAllText(Path.Combine(host.PublishDirectory, "functions.metadata")));
        var published = metadata.RootElement.EnumerateArray()
            .Select(function => function.GetProperty("name").GetString())
            .ToList();

        published.ShouldBe(Probes.Select(row => (string?)row[0]).Concat(InsightsFunctions), ignoreOrder: true);
    }
}
