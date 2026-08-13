using FellowshipAnalyzer.Core.UI.Components;
using Shouldly;
using Xunit;

namespace FellowshipAnalyzer.Core.Tests.UI;

public class ErrorReportTests
{
    [Theory]
    [InlineData("/report/87xnBZqymNLHvk3X", "87xnBZqymNLHvk3X")]
    [InlineData("/report/87xnBZqymNLHvk3X/5", "87xnBZqymNLHvk3X")]
    [InlineData("/report/87xnBZqymNLHvk3X/5/25", "87xnBZqymNLHvk3X")]
    [InlineData("report/87xnBZqymNLHvk3X/5", "87xnBZqymNLHvk3X")]
    [InlineData("/report/87xnBZqymNLHvk3X?tab=guide", "87xnBZqymNLHvk3X")]
    public void ReportCode_IsTakenFromTheReportPath(string relativePath, string expected)
    {
        Report(new InvalidOperationException("boom"), relativePath).ReportCode.ShouldBe(expected);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/character/42")]
    [InlineData("/report/")]
    public void ReportCode_IsNull_AwayFromAReportPage(string relativePath)
    {
        Report(new InvalidOperationException("boom"), relativePath).ReportCode.ShouldBeNull();
    }

    [Fact]
    public void Component_IsTheDeepestProjectFrame()
    {
        var exception = Thrown();

        var component = Report(exception).Component;

        component.ShouldNotBeNull();
        component.ShouldContain("FellowshipAnalyzer");
        component.ShouldNotContain("(");
    }

    [Fact]
    public void Component_IsNull_WhenNothingWasThrown()
    {
        Report(new InvalidOperationException("never thrown")).Component.ShouldBeNull();
    }

    [Fact]
    public void Detail_CarriesTheInnerException()
    {
        var exception = new InvalidOperationException("outer", new ArgumentNullException("key"));

        var detail = Report(exception).Detail;

        detail.ShouldContain("outer");
        detail.ShouldContain("key");
    }

    [Fact]
    public void Title_IsTheExceptionTypeName()
    {
        Report(new ArgumentException("bad")).Title.ShouldBe(nameof(ArgumentException));
    }

    [Fact]
    public void IssueUrl_PrefillsThePageReportAndException()
    {
        var report = Report(new ArgumentException("An item with the same key has already been added."));

        var body = Uri.UnescapeDataString(QueryValue(report.IssueUrl, "body"));

        body.ShouldContain("https://www.fellowshipanalyzer.com/report/87xnBZqymNLHvk3X/5");
        body.ShouldContain("`87xnBZqymNLHvk3X`");
        body.ShouldContain("```text");
        body.ShouldContain("An item with the same key has already been added.");
    }

    [Fact]
    public void IssueUrl_TitlesTheIssueWithTheTypeAndFirstMessageLine()
    {
        var report = Report(new ArgumentException("first line\nsecond line"));

        var title = Uri.UnescapeDataString(QueryValue(report.IssueUrl, "title"));

        title.ShouldBe("ArgumentException: first line");
    }

    [Fact]
    public void IssueUrl_TruncatesALongException()
    {
        var report = Report(new InvalidOperationException(new string('x', 12_000)));

        report.IssueUrl.Length.ShouldBeLessThan(8_000);
        report.Detail.ShouldContain(new string('x', 12_000));
    }

    [Fact]
    public void IssueUrl_TargetsTheProjectRepository()
    {
        Report(new InvalidOperationException("boom")).IssueUrl
            .ShouldStartWith("https://github.com/Seriousnes/FellowshipAnalyzer/issues/new?");
    }

    private static ErrorReport Report(Exception exception, string relativePath = "/report/87xnBZqymNLHvk3X/5") =>
        new(exception, $"https://www.fellowshipanalyzer.com{relativePath}", relativePath);

    private static Exception Thrown()
    {
        try
        {
            throw new InvalidOperationException("boom");
        }
        catch (InvalidOperationException exception)
        {
            return exception;
        }
    }

    private static string QueryValue(string url, string name)
    {
        var query = url[(url.IndexOf('?') + 1)..];
        foreach (var pair in query.Split('&'))
        {
            var separator = pair.IndexOf('=');
            if (separator > 0 && pair[..separator] == name)
                return pair[(separator + 1)..];
        }

        return "";
    }
}
