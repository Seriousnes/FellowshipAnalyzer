using FellowshipAnalyzer.SpellData;
using FellowshipAnalyzer.SpellData.Model;

namespace FellowshipAnalyzer.SpellStudio;

public sealed class MergeService
{
    public MergeResult Result { get; } = MergeEngine.Run(MergeInputs.Load());
}
