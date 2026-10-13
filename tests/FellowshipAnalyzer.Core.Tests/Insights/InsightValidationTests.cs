using FellowshipAnalyzer.Core.Insights;

using Shouldly;

using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Insights;

public class InsightValidationTests
{
    private static readonly Playbook Aeona = Playbook.Load("Aeona")!;

    [Fact]
    public void Check_DropsInsightsWithNumbersIdsOrForbiddenTermsAndKeepsThree()
    {
        GuideInsight[] insights =
        [
            new("Cleanses on thin Stagger", "You cleansed thin Stagger. Cast Oblivion under 40% Stagger."),
            new("Overheal", "You overhealed 77,777. Hold the cleanse."),
            new("Provenance", "The log shows thin cleanses. Cast Oblivion."),
            new("Pull id", "In pull3 you cleansed early. Cast Oblivion."),
            new("Second", "Cast Oblivion."),
            new("Third", "Cast Oblivion."),
            new("Fourth", "Cast Oblivion."),
        ];

        var result = InsightValidation.Check(insights, Aeona, "staggerCleanse");

        result.Accepted.Select(insight => insight.Title).ShouldBe(["Cleanses on thin Stagger", "Second", "Third"]);
        result.Rejections.Select(rejection => (rejection.Index, rejection.Reason)).ShouldBe(
            [(1, "number-not-in-playbook"), (2, "forbidden-term"), (3, "id"), (6, "beyond-limit")]);
    }

    [Fact]
    public void ForbiddenTerms_ReadsOnlyTheQuotedList() =>
        InsightValidation.ForbiddenTerms(Aeona).ShouldBe(["reconstructed", "estimated", "recorded", "the log", "the data"]);
}
