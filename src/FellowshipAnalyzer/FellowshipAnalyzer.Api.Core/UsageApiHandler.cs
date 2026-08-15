using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace FellowshipAnalyzer.Api.Core;

public sealed class UsageApiHandler(ILogger<UsageApiHandler> logger)
{
    private const int MaxPathLength = 200;
    private const int MaxHeroLength = 32;

    [ApiEndpoint("POST", "track")]
    public Task<IResult> TrackAsync(HttpContext context, string? path, string? hero)
    {
        context.Response.Headers.CacheControl = "no-store";

        if (SanitizePath(path) is not { } safePath)
        {
            return Task.FromResult(Results.NoContent());
        }

        if (SanitizeHero(hero) is { } safeHero)
        {
            logger.LogInformation("Usage hero {Hero} {Path}", safeHero, safePath);
        }
        else
        {
            logger.LogInformation("Usage page {Path}", safePath);
        }

        return Task.FromResult(Results.NoContent());
    }

    public static string? SanitizePath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaxPathLength || value[0] is not '/')
        {
            return null;
        }

        foreach (var character in value)
        {
            if (!char.IsAsciiLetterOrDigit(character) && character is not ('/' or '-' or '_' or '.' or ':' or '~'))
            {
                return null;
            }
        }

        return value;
    }

    public static string? SanitizeHero(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaxHeroLength)
        {
            return null;
        }

        foreach (var character in value)
        {
            if (!char.IsAsciiLetter(character))
            {
                return null;
            }
        }

        return value.ToLowerInvariant();
    }
}
