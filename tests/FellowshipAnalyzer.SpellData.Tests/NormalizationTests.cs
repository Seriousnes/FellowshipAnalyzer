using FellowshipAnalyzer.SpellData;
using Shouldly;
using Xunit;

namespace FellowshipAnalyzer.SpellData.Tests;

public class NormalizationTests
{
    private static readonly System.Collections.Generic.Dictionary<string, double> FreezingTorrent = new()
    {
        ["Cooldown"] = 15.0, ["MaxRange"] = 3000.0, ["ChannelingDuration"] = 2.0, ["ChannelingTickInterval"] = 0.4,
    };

    [Fact]
    public void Maps_FreezingTorrentScalars()
    {
        Normalization.Cooldown(FreezingTorrent).ShouldBe(15.0);
        Normalization.Range(FreezingTorrent).ShouldBe(30);
        Normalization.ChannelDuration(FreezingTorrent).ShouldBe(2.0);
        Normalization.ChannelTickInterval(FreezingTorrent).ShouldBe(0.4);
        Normalization.Charges(FreezingTorrent).ShouldBe(1);
    }

    [Fact]
    public void Charges_PrefersMaxThenNum()
    {
        Normalization.Charges(new Dictionary<string, double> { ["MaxCharges"] = 3 }).ShouldBe(3);
        Normalization.Charges(new Dictionary<string, double> { ["NumCharges"] = 2 }).ShouldBe(2);
    }
}
