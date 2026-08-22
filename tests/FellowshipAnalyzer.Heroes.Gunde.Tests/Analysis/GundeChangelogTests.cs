using FellowshipAnalyzer.Core.Analysis;
using FellowshipAnalyzer.Heroes.Gunde.Analysis;

using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.AspNetCore.Components.Rendering;

using Shouldly;

using Xunit;

namespace FellowshipAnalyzer.Heroes.Gunde.Tests.Analysis;

public sealed class GundeChangelogTests
{
    public static TheoryData<ChangelogEntry> Entries()
    {
        var data = new TheoryData<ChangelogEntry>();
        foreach (var entry in GundeCombatLogParser.HeroConfig.Changelog)
        {
            data.Add(entry);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Entries))]
    public void EntryRendersOnlyNamedElements(ChangelogEntry entry)
    {
        using var builder = new RenderTreeBuilder();
        entry.Changes(builder);

        var frames = builder.GetFrames();
        for (var i = 0; i < frames.Count; i++)
        {
            var frame = frames.Array[i];
            if (frame.FrameType == RenderTreeFrameType.Element)
            {
                frame.ElementName.ShouldNotBeNullOrWhiteSpace(
                    $"Changelog entry dated {entry.Date:yyyy-MM-dd} opens an element with no tag name.");
            }
        }
    }
}
