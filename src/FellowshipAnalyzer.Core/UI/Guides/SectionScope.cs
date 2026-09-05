namespace FellowshipAnalyzer.Core.UI.Guides;

/// <summary>
/// The activity ledger a <see cref="Section"/> cascades to the content nested in it. Each member
/// reports whether it is active; the section reads <see cref="HasContent"/> to decide whether to
/// render at all, so a section whose every member suppressed itself shows no header.
/// </summary>
/// <param name="onChanged">Invoked when <see cref="HasContent"/> changes, to re-render the section.</param>
public sealed class SectionScope(Action onChanged)
{
    private readonly HashSet<object> _reported = [];
    private readonly HashSet<object> _active = [];

    /// <summary>
    /// <c>true</c> while no member has reported, so content that does not report always renders, and
    /// <c>true</c> once at least one reporting member is active.
    /// </summary>
    public bool HasContent => _reported.Count == 0 || _active.Count > 0;

    /// <summary>
    /// Records <paramref name="member"/>'s current activity. Called on every parameter set, so a
    /// member that becomes active or inactive on a later render is counted correctly.
    /// </summary>
    public void Report(object member, bool active)
    {
        var before = HasContent;

        _reported.Add(member);
        if (active) _active.Add(member);
        else _active.Remove(member);

        if (HasContent != before) onChanged();
    }
}
