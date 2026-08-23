using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace FellowshipAnalyzer.Api.Core;

public static class FellowshipLogsApiBuilderExtensions
{
    /// <summary>
    /// Name of the CORS policy registered by <c>AddFellowshipLogsApi</c>.
    /// </summary>
    public const string CorsPolicyName = "FellowshipLogs";

    /// <summary>
    /// Explicitly applies the FellowshipLogs API request pipeline (CORS + security headers).
    /// Hosts that build a full ASP.NET Core pipeline (e.g. DevApi via <see cref="WebApplication"/>)
    /// can call this for clarity. The Azure Functions host gets the same middleware automatically
    /// through the <see cref="FellowshipLogsApiStartupFilter"/> registered by
    /// <c>AddFellowshipLogsApi</c>.
    /// </summary>
    public static IApplicationBuilder UseFellowshipLogsApi(this IApplicationBuilder app)
    {
        ApplyMiddleware(app);
        return app;
    }

    internal static void ApplyMiddleware(IApplicationBuilder app)
    {
        app.UseCors(CorsPolicyName);
        app.Use(static async (context, next) =>
        {
            context.Response.OnStarting(static state =>
            {
                var response = (HttpResponse)state;
                response.Headers.XContentTypeOptions = "nosniff";
                response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
                return Task.CompletedTask;
            }, context.Response);

            await next();
        });
    }
}

/// <summary>
/// Ensures the FellowshipLogs API CORS + security middleware is wired into the ASP.NET Core
/// pipeline regardless of host. Required for the Azure Functions Worker AspNetCore integration,
/// which does not expose <see cref="IApplicationBuilder"/> directly to user code.
/// </summary>
internal sealed class FellowshipLogsApiStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
        app =>
        {
            FellowshipLogsApiBuilderExtensions.ApplyMiddleware(app);
            next(app);
        };
}
