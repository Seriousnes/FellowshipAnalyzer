using FellowshipAnalyzer.Core.Analysis;

namespace FellowshipAnalyzer.Core.UI.Components;

/// <summary>
/// Coordinates the single, app-level contributor profile modal. <see cref="ContributorLink"/>
/// calls <see cref="Show"/>; one <c>ContributorProfileModal</c> host at the layout root renders it.
/// Keeping the modal at the root (rather than inside a hero card) keeps its <c>position: fixed</c>
/// anchored to the viewport, since a transformed ancestor would otherwise become its containing block.
/// </summary>
public sealed class ContributorModalService
{
    /// <summary>The contributor whose profile is open, or <c>null</c> when the modal is closed.</summary>
    public Contributor? Current { get; private set; }

    /// <summary>Raised whenever <see cref="Current"/> changes.</summary>
    public event Action? Changed;

    public void Show(Contributor contributor)
    {
        Current = contributor;
        Changed?.Invoke();
    }

    public void Close()
    {
        if (Current is null)
            return;
        Current = null;
        Changed?.Invoke();
    }
}
