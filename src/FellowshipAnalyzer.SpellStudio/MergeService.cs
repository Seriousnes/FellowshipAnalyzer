using FellowshipAnalyzer.SpellData;
using FellowshipAnalyzer.SpellData.Model;
using FellowshipAnalyzer.SpellData.Sources;

namespace FellowshipAnalyzer.SpellStudio;

public sealed class MergeService
{
    private MergeInputs _inputs;
    private MergeResult _result;

    public MergeService()
    {
        _inputs = MergeInputs.Load();
        _result = MergeEngine.Run(_inputs);
    }

    public MergeResult Result => _result;
    public OverridesSource Overrides => _inputs.Overrides;

    public void Reload()
    {
        _inputs = MergeInputs.Load();
        _result = MergeEngine.Run(_inputs);
    }
}
