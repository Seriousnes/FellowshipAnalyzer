using FellowshipAnalyzer.Core.Common.Spells;

using Shouldly;

using Xunit;

using Talents = FellowshipAnalyzer.Core.Common.Spells.Gunde.Talents;

namespace FellowshipAnalyzer.Heroes.Gunde.Tests.Analysis;

public sealed class GundeTalentTests
{
    [Fact]
    public void GeneratedTalentConstants_MatchTheHandWrittenTalents()
    {
        GundeTalents.DeepRend.ShouldBe(751);
        GundeTalents.MurderOfCrows.ShouldBe(758);
        GundeTalents.Massacre.ShouldBe(772);

        GundeTalents.DeepRend.ShouldBe(Talents.DeepRend.Id);
        GundeTalents.MurderOfCrows.ShouldBe(Talents.MurderOfCrows.Id);
        GundeTalents.Massacre.ShouldBe(Talents.Massacre.Id);
    }

    [Fact]
    public void TalentFslids_LandInTheTalentIdRange()
    {
        Talents.DeepRend.FSLID.Value.ShouldBe(2_000_751);
        Talents.CrimsonStrikes.Name.ShouldBe("Crimson strikes");
    }
}
