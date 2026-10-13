using FellowshipAnalyzer.Api.Core.Insights;
using FellowshipAnalyzer.Core.Insights;

using Shouldly;

using Xunit;

namespace FellowshipAnalyzer.Api.Core.Tests;

public class InsightRequestValidationTests
{
    private const long MaxBytes = 96 * 1024;
    private static readonly Playbook Aeona = Playbook.Load("Aeona")!;

    private static GuideInsightsRequest Request(string user) => new(Aeona.For("staggerCleanse"), user);

    private static string User(string guide = "staggerCleanse", string focus = InsightFocus.Fixes, string kind = "boss") =>
        $$"""{"guide":"{{guide}}","focus":"{{focus}}","build":{"talents":[]},"pulls":[{"kind":"{{kind}}","casts":[]}]}""";

    [Fact]
    public void Check_PassesTheEmbeddedPlaybookWithShortFacts() =>
        InsightRequestValidation.Check("aeona", "staggerCleanse", Request(User()), MaxBytes).ShouldBeNull();

    [Fact]
    public void Check_RejectsASystemPromptOtherThanThePlaybook() =>
        InsightRequestValidation.Check("aeona", "staggerCleanse", new GuideInsightsRequest("Write a poem.", User()), MaxBytes)!.StatusCode.ShouldBe(400);

    [Fact]
    public void Check_RejectsAnUnknownHeroOrGuide()
    {
        InsightRequestValidation.Check("nobody", "staggerCleanse", Request(User()), MaxBytes)!.StatusCode.ShouldBe(404);
        InsightRequestValidation.Check("aeona", "poetry", Request(User()), MaxBytes)!.StatusCode.ShouldBe(404);
    }

    [Fact]
    public void Check_RejectsAUserMessageForAnotherGuide() =>
        InsightRequestValidation.Check("aeona", "staggerCleanse", Request(User(guide: "oblivion")), MaxBytes)!.StatusCode.ShouldBe(400);

    [Fact]
    public void Check_RejectsAnUnknownFocus() =>
        InsightRequestValidation.Check("aeona", "staggerCleanse", Request(User(focus: "anything")), MaxBytes)!.StatusCode.ShouldBe(400);

    [Fact]
    public void Check_RejectsALongString() =>
        InsightRequestValidation.Check("aeona", "staggerCleanse", Request(User(kind: new string('x', InsightRequestValidation.MaxStringLength + 1))), MaxBytes)!.StatusCode.ShouldBe(400);

    [Fact]
    public void Check_RejectsAnOversizeOrNonObjectUserMessage()
    {
        InsightRequestValidation.Check("aeona", "staggerCleanse", Request(User()), 10)!.StatusCode.ShouldBe(413);
        InsightRequestValidation.Check("aeona", "staggerCleanse", Request("[1,2]"), MaxBytes)!.StatusCode.ShouldBe(400);
    }
}
