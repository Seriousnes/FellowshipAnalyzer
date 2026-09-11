using Fellowship.SDK;
using Fellowship.SDK.Client;

using FellowshipAnalyzer.Core.Game;
using FellowshipAnalyzer.Core.UI.Components;

using Shouldly;

using Xunit;

namespace FellowshipAnalyzer.Core.Tests.UI;

/// <summary>
/// Groups every test that reads the codex a record addresses. <see cref="Codex.Use"/> states it for
/// the whole process, so these run one at a time rather than alongside each other.
/// </summary>
[CollectionDefinition(Name)]
public sealed class CodexCollection
{
    public const string Name = "Codex";
}

[Collection(CodexCollection.Name)]
public class CodexTests
{
    private const string Textures = "https://cdn.codex.fellowshipanalyzer.com/ui";

    public CodexTests() => Codex.Use(CodexOptions.Default);

    [Fact]
    public void PageUrl_AddressesTheCodexPage() =>
        Codex.PageUrl(CodexAddresses.Page(EntityType.Ability, 1964))
            .ShouldBe("https://codex.fellowshipanalyzer.com/ability/1964");

    [Theory]
    [InlineData("T_Lisa_Stagger.jpg")]
    [InlineData("T_Lisa_Stagger.png")]
    [InlineData("T_Lisa_Stagger")]
    [InlineData("effects/T_Lisa_Stagger.jpg")]
    public void IconUrl_NormalisesEveryTextureNameToOneAddress(string icon) =>
        Codex.IconUrl(icon).ShouldBe($"{Textures}/T_Lisa_Stagger_full.webp");

    [Theory]
    [InlineData(3, "epic")]
    [InlineData(4, "champion")]
    [InlineData(5, "heroic")]
    [InlineData(6, "legendary")]
    public void IconUrl_EndsRankedTextureInTheRungsStoredName(int tier, string expected) =>
        Codex.IconUrl("Icon_Rime_ArcticOwl_Head_R1_T0.jpg", tier)
            .ShouldBe($"{Textures}/Icon_Rime_ArcticOwl_Head_R1_T0-{expected}_full.webp");

    [Fact]
    public void IconUrl_LeavesTextureUnrankedWhenTheLadderHasNoSuchRung() =>
        Codex.IconUrl("Tex_rings_07_b.jpg", 99)
            .ShouldBe($"{Textures}/Tex_rings_07_b_full.webp");

    [Theory]
    [InlineData("Tex_bracers_09_b.jpg")]
    [InlineData("Tex_necklace_03_b.jpg")]
    [InlineData("T_Icons_Gems_Sapphire3.png")]
    public void IconUrl_LeavesTextureSharedAcrossRungsBare(string icon) =>
        Codex.IconUrl(icon, 5).ShouldBe(Codex.IconUrl(icon));

    [Fact]
    public void IconUrl_RanksTextureDrawnPerRung() =>
        Codex.IconUrl("Icon_Rime_ArcticOwl_Head_R1_T0.jpg", 5)
            .ShouldBe($"{Textures}/Icon_Rime_ArcticOwl_Head_R1_T0-heroic_full.webp");

    [Fact]
    public void Use_DropsATrailingSlash()
    {
        Codex.Use(new CodexOptions { Origin = "http://localhost:5488/", TextureOrigin = "http://localhost:5488/" });

        Codex.PageUrl(CodexAddresses.Page(EntityType.Ability, 1964))
            .ShouldBe("http://localhost:5488/ability/1964");
        Codex.IconUrl("T_Lisa_Stagger.jpg")
            .ShouldBe("http://localhost:5488/ui/T_Lisa_Stagger_full.webp");
    }

    [Theory]
    [InlineData(23, "Codex_CapDungeon_VV")]
    [InlineData(31, "Codex_Dungeon_SP")]
    public void DungeonIconUrl_AddressesTheZonesTexture(int zoneId, string expected) =>
        Dungeons.IconFor(zoneId).ShouldBe(expected);

    [Theory]
    [InlineData(26)]
    [InlineData(9999)]
    public void DungeonTexture_IsNull_WhenTheGameDataDrawsNoDungeon(int zoneId) =>
        Dungeons.IconFor(zoneId).ShouldBeNull();
}
