using FellowshipAnalyzer.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddHttpClient("DevApi", client =>
{
    client.BaseAddress = new Uri("https+http://fellowshipanalyzerapi/");
});

var app = builder.Build();

app.MapDefaultEndpoints();

if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

app.MapMethods(
    "/api/{**path}",
    ["DELETE", "GET", "HEAD", "OPTIONS", "PATCH", "POST", "PUT"],
    ProxyApiRequestAsync);

app.MapFallbackToFile("index.html");

app.Run();

static async Task ProxyApiRequestAsync(HttpContext context, IHttpClientFactory httpClientFactory)
{
    var targetUri = context.Request.Path + context.Request.QueryString;
    using var requestMessage = new HttpRequestMessage(new HttpMethod(context.Request.Method), targetUri);

    if (RequestHasBody(context.Request))
    {
        requestMessage.Content = new StreamContent(context.Request.Body);
    }

    foreach (var header in context.Request.Headers)
    {
        if (ShouldSkipRequestHeader(header.Key))
        {
            continue;
        }

        if (!requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray()))
        {
            requestMessage.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
        }
    }

    var httpClient = httpClientFactory.CreateClient("DevApi");
    using var responseMessage = await httpClient.SendAsync(
        requestMessage,
        HttpCompletionOption.ResponseHeadersRead,
        context.RequestAborted);

    context.Response.StatusCode = (int)responseMessage.StatusCode;

    foreach (var header in responseMessage.Headers)
    {
        context.Response.Headers[header.Key] = header.Value.ToArray();
    }

    foreach (var header in responseMessage.Content.Headers)
    {
        context.Response.Headers[header.Key] = header.Value.ToArray();
    }

    foreach (var headerName in GetHopByHopHeaderNames())
    {
        context.Response.Headers.Remove(headerName);
    }

    await responseMessage.Content.CopyToAsync(context.Response.Body, context.RequestAborted);
}

static bool RequestHasBody(HttpRequest request)
    => request.ContentLength is > 0 || request.Headers.ContainsKey("Transfer-Encoding");

static bool ShouldSkipRequestHeader(string headerName)
    => string.Equals(headerName, "Host", StringComparison.OrdinalIgnoreCase)
        || IsHopByHopHeader(headerName);

static bool IsHopByHopHeader(string headerName)
{
    foreach (var hopByHopHeaderName in GetHopByHopHeaderNames())
    {
        if (string.Equals(headerName, hopByHopHeaderName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
    }

    return false;
}

static string[] GetHopByHopHeaderNames() =>
[
    "Connection",
    "Keep-Alive",
    "Proxy-Authenticate",
    "Proxy-Authorization",
    "TE",
    "Trailer",
    "Transfer-Encoding",
    "Upgrade"
];
