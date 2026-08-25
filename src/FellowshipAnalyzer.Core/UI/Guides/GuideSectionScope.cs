namespace FellowshipAnalyzer.Core.UI.Guides;

/// <summary>
/// The activity ledger a <see cref="Section"/> cascades to the guides nested in it. Each guide
/// reports whether it is active; the section reads <see cref="HasContent"/> to decide whether to
/// render at all, so a section whose every guide suppressed itself shows no header.
/// </summary>
/// <param name="onChanged">Invoked when <see cref="HasContent"/> changes, to re-render the section.</param>
public sealed class GuideSectionScope(Action onChanged)
{
    private readonly HashSet<object> _reported = [];
    private readonly HashSet<object> _active = [];

    /// <summary>
    /// <c>true</c> while no guide has reported, so content that is not a guide always renders, and
    /// <c>true</c> once at least one reporting guide is active.
    /// </summary>
    public bool HasContent => _reported.Count == 0 || _active.Count > 0;

    /// <summary>
    /// Records <paramref name="guide"/>'s current activity. Called on every parameter set, so a guide
    /// that becomes active or inactive on a later render is counted correctly.
    /// </summary>
    public void Report(object guide, bool active)
    {
        var before = HasContent;

        _reported.Add(guide);
        if (active) _active.Add(guide);
        else _active.Remove(guide);

        if (HasContent != before) onChanged();
    }
}
