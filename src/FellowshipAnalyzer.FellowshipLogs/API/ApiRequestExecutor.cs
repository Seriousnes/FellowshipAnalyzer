using System.Net.Http.Json;
using System.Text.Json;

namespace FellowshipAnalyzer.FellowshipLogs.API;

internal interface IApiRequestExecutor
{
    Task<T> ExecuteAsync<T>(HttpRequestMessage request, CancellationToken cancellationToken = default);
    HttpContent SerializeContent(object requestContent);
}

internal sealed class ApiRequestExecutor(HttpClient httpClient, JsonSerializerOptions jsonOptions) : IApiRequestExecutor
{
    public async Task<T> ExecuteAsync<T>(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<T>(stream, jsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("The API returned a null response.");
    }

    public HttpContent SerializeContent(object requestContent) =>
        JsonContent.Create(requestContent, options: jsonOptions);
}
