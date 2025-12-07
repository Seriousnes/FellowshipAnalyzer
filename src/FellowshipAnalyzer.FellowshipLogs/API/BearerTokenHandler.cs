using System.Net.Http.Headers;

namespace FellowshipAnalyzer.FellowshipLogs.API;

internal sealed class BearerTokenHandler(ClientCredentialsTokenCache tokenCache) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var token = await tokenCache.GetTokenAsync(cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await base.SendAsync(request, cancellationToken);
    }
}
