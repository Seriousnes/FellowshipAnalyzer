using System.Text;

namespace FellowshipAnalyzer.Core.UI.Components;

/// <summary>
/// Everything known about an unhandled page exception: the exception itself, where it happened, and
/// the prefilled GitHub issue URL that reports it.
/// </summary>
public sealed class ErrorReport
{
    private const string RepositoryUrl = "https://github.com/Seriousnes/FellowshipAnalyzer";
    private const string ProjectNamespace = "FellowshipAnalyzer";
    private const int MaximumIssueDetailLength = 4000;
    private const int MaximumIssueTitleLength = 120;

    /// <summary>Describes an unhandled exception raised while rendering a page.</summary>
    /// <param name="exception">The unhandled exception.</param>
    /// <param name="url">Absolute URL of the page that failed.</param>
    /// <param name="relativePath">Path and query of that page, without the origin.</param>
    public ErrorReport(Exception exception, string url, string relativePath)
    {
        Exception = exception;
        Url = url;
        RelativePath = relativePath;
        ReportCode = ExtractReportCode(relativePath);
        Component = ExtractComponent(exception);
        Detail = exception.ToString();
    }

    /// <summary>The unhandled exception this report describes.</summary>
    public Exception Exception { get; }

    /// <summary>Absolute URL of the page that failed.</summary>
    public string Url { get; }

    /// <summary>Path and query of the page that failed, without the origin.</summary>
    public string RelativePath { get; }

    /// <summary>Report code taken from a <c>/report/{code}</c> path, or <see langword="null"/> elsewhere.</summary>
    public string? ReportCode { get; }

    /// <summary>
    /// Deepest project method on the stack, searched from the innermost exception outwards, or
    /// <see langword="null"/> when no frame names the project.
    /// </summary>
    public string? Component { get; }

    /// <summary>Full exception text: type, message, stack trace, and every inner exception.</summary>
    public string Detail { get; }

    /// <summary>Exception type name, used as the dialog heading.</summary>
    public string Title => Exception.GetType().Name;

    /// <summary>
    /// GitHub "new issue" URL for this failure, prefilled with the page, report code, component, and
    /// exception. The exception is truncated so the URL stays within what GitHub accepts.
    /// </summary>
    public string IssueUrl =>
        $"{RepositoryUrl}/issues/new?title={Uri.EscapeDataString(BuildIssueTitle())}&body={Uri.EscapeDataString(BuildIssueBody())}";

    private string BuildIssueTitle()
    {
        var message = FirstLine(Exception.Message);
        var title = string.IsNullOrEmpty(message) ? Title : $"{Title}: {message}";
        return Truncate(title, MaximumIssueTitleLength);
    }

    private string BuildIssueBody()
    {
        var builder = new StringBuilder();
        builder.AppendLine("### What happened");
        builder.AppendLine();
        builder.AppendLine("<!-- What were you doing when this happened? -->");
        builder.AppendLine();
        builder.AppendLine("### Details");
        builder.AppendLine();
        builder.AppendLine($"- **Page**: {Url}");

        if (ReportCode is { } reportCode)
            builder.AppendLine($"- **Report**: `{reportCode}`");

        if (Component is { } component)
            builder.AppendLine($"- **Component**: `{component}`");

        builder.AppendLine();
        builder.AppendLine("### Exception");
        builder.AppendLine();
        builder.AppendLine("```text");
        builder.AppendLine(Truncate(Detail, MaximumIssueDetailLength));
        builder.AppendLine("```");
        return builder.ToString();
    }

    private static string? ExtractReportCode(string relativePath)
    {
        var path = relativePath.AsSpan();

        var query = path.IndexOfAny('?', '#');
        if (query >= 0)
            path = path[..query];

        path = path.Trim('/');
        if (!path.StartsWith("report/", StringComparison.OrdinalIgnoreCase))
            return null;

        var code = path["report/".Length..];
        var separator = code.IndexOf('/');
        if (separator >= 0)
            code = code[..separator];

        return code.IsEmpty ? null : code.ToString();
    }

    private static string? ExtractComponent(Exception exception)
    {
        foreach (var candidate in FromInnermost(exception))
        {
            foreach (var line in EnumerateLines(candidate.StackTrace))
            {
                var trimmed = line.Trim();
                if (!trimmed.Contains(ProjectNamespace, StringComparison.Ordinal))
                    continue;

                const string at = "at ";
                if (trimmed.StartsWith(at, StringComparison.Ordinal))
                    trimmed = trimmed[at.Length..];

                var arguments = trimmed.IndexOf('(');
                if (arguments > 0)
                    trimmed = trimmed[..arguments];

                return trimmed.Trim();
            }
        }

        return null;
    }

    private static List<Exception> FromInnermost(Exception exception)
    {
        List<Exception> chain = [];
        for (Exception? current = exception; current is not null; current = current.InnerException)
            chain.Add(current);

        chain.Reverse();
        return chain;
    }

    private static string[] EnumerateLines(string? text) =>
        string.IsNullOrEmpty(text) ? [] : text.Split('\n');

    private static string FirstLine(string text)
    {
        var end = text.IndexOfAny(['\r', '\n']);
        return (end < 0 ? text : text[..end]).Trim();
    }

    private static string Truncate(string text, int length) =>
        text.Length <= length ? text : $"{text[..length]}…";
}
