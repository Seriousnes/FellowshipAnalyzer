using System.Net;
using System.Text;

using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.FellowshipLogs;
using FellowshipAnalyzer.FellowshipLogs.API;

using Shouldly;

using Xunit;

namespace FellowshipAnalyzer.FellowshipLogs.Tests;

public sealed class FellowshipLogsClientTests
{
    [Fact]
    public async Task GetEventsAsync_FetchesAllEvents()
    {
        var handler = new QueueMessageHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"access_token\":\"token-123\",\"expires_in\":3600}", Encoding.UTF8, "application/json")
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"data":{"reportData":{"report":{"events":{"data":[{"timestamp":1,"type":"damage","sourceID":1,"targetID":2,"abilityGameID":100,"fight":1,"hitType":1,"amount":1000},{"timestamp":2,"type":"cast","sourceID":1,"abilityGameID":200,"fight":1},{"timestamp":3,"type":"heal","sourceID":1,"targetID":2,"abilityGameID":300,"fight":1,"hitType":1,"amount":500}]}}}}}""",
                    Encoding.UTF8,
                    "application/json")
            });

        using var httpClient = new HttpClient(handler);
        var jsonOptions = ServiceCollectionExtensions.CreateJsonSerializerOptions();
        var apiExecutor = new ApiRequestExecutor(httpClient, jsonOptions);
        var options = new FellowshipLogsClientOptions
        {
            ClientId = "id",
            ClientSecret = "secret",
            TokenEndpoint = "https://token.test/oauth/token",
            GraphQlEndpoint = "https://api.test/graphql"
        };
        var client = new ApiClient(apiExecutor, options);

        var events = await client.Events.GetAsync(new FellowshipLogsEventsRequest("abc123", 12, 34));

        events.Events.Count.ShouldBe(3);
        events.Events[0].ShouldBeOfType<DamageEvent>();
        events.Events[0].Timestamp.ShouldBe(1);
        events.Events[1].ShouldBeOfType<CastEvent>();
        events.Events[1].Timestamp.ShouldBe(2);
        events.Events[2].ShouldBeOfType<HealEvent>();
        events.Events[2].Timestamp.ShouldBe(3);
        events.InProgress.ShouldBeFalse();
    }

    private sealed class QueueMessageHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_responses.Count == 0)
            {
                throw new InvalidOperationException("No queued response available for request.");
            }

            return Task.FromResult(_responses.Dequeue());
        }
    }
}
