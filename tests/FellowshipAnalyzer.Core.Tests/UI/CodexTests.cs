using Fellowship.SDK;
using Fellowship.SDK.Client;

using FellowshipAnalyzer.Core.UI.Components;

using Shouldly;

using Xunit;

namespace FellowshipAnalyzer.Core.Tests.UI;

public class CodexTests
{
    [Fact]
    public void PageUrl_AddressesTheCodexPage() =>
        Codex.PageUrl(CodexAddresses.Page(EntityType.Ability, 1964))
            .ShouldBe("https://codex.fellowshipanalyzer.com/ability/1964");

    [Theory]
    [InlineData("T_Lisa_Stagger.jpg")]
    [InlineData("T_Lisa_Stagger.png")]
    [InlineData("T_Lisa_Stagger")]
    [InlineData("effects/T_Lisa_Stagger.jpg")]
    public void IconUrl_NormalisesEveryArtNameToOnePngAddress(string icon) =>
        Codex.IconUrl(icon).ShouldBe("https://cdn.codex.fellowshipanalyzer.com/ui/T_Lisa_Stagger.png");

    [Theory]
    [InlineData(3, "epic")]
    [InlineData(4, "champion")]
    [InlineData(5, "heroic")]
    [InlineData(6, "legendary")]
    public void IconUrl_EndsRankedArtInTheRungsStoredName(int tier, string expected) =>
        Codex.IconUrl("Icon_Rime_ArcticOwl_Head_R1_T0.jpg", tier)
            .ShouldBe($"https://cdn.codex.fellowshipanalyzer.com/ui/Icon_Rime_ArcticOwl_Head_R1_T0-{expected}.png");

    [Fact]
    public void IconUrl_LeavesArtUnrankedWhenTheLadderHasNoSuchRung() =>
        Codex.IconUrl("Tex_rings_07_b.jpg", 99)
            .ShouldBe("https://cdn.codex.fellowshipanalyzer.com/ui/Tex_rings_07_b.png");

    [Theory]
    [InlineData("Tex_bracers_09_b.jpg")]
    [InlineData("Tex_necklace_03_b.jpg")]
    [InlineData("T_Icons_Gems_Sapphire3.png")]
    public void IconUrl_LeavesArtSharedAcrossRungsBare(string icon) =>
        Codex.IconUrl(icon, 5).ShouldBe(Codex.IconUrl(icon));

    [Fact]
    public void IconUrl_RanksArtDrawnPerRung() =>
        Codex.IconUrl("Icon_Rime_ArcticOwl_Head_R1_T0.jpg", 5)
            .ShouldBe("https://cdn.codex.fellowshipanalyzer.com/ui/Icon_Rime_ArcticOwl_Head_R1_T0-heroic.png");
}
