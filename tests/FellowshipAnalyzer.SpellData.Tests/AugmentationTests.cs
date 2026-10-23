using FellowshipAnalyzer.SpellData;
using FellowshipAnalyzer.SpellData.Sources;

using Shouldly;

using Xunit;

namespace FellowshipAnalyzer.SpellData.Tests;

public class AugmentationTests
{
    [Fact]
    public void Run_GivesEachAugmentationTheModifierSlotItsCategoryFills()
    {
        var result = MergeEngine.Run(MergeInputs.Load());

        result.Augmentations.Single(a => a.Id == 42).ShouldBe(new ExportAugmentation(42, "Sigil", "HeroicTrait"));
        result.Augmentations.Single(a => a.Id == 51).ShouldBe(new ExportAugmentation(51, "Aspect", "MajorTrait"));
    }

    [Fact]
    public void Run_GivesAProtectiveEnchantNoModifierSlot()
    {
        var result = MergeEngine.Run(MergeInputs.Load());

        result.Augmentations.Single(a => a.Id == 52).ShouldBe(new ExportAugmentation(52, "ProtectiveEnchant", null));
    }

    [Fact]
    public void Deserialize_RoundTripsTheAugmentationsSection()
    {
        var original = MergeEngine.Run(MergeInputs.Load());
        var restored = SpellDbWriter.Deserialize(SpellDbWriter.Serialize(original));

        restored.Augmentations.ShouldBe(original.Augmentations, ignoreOrder: true);
    }
}
