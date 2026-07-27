namespace FellowshipAnalyzer.Core.UI.Components;

/// <summary>
/// Visual style for a <see cref="Tabs"/> control. Underline matches the report top-tab
/// chrome; Pill is the About sub-navigation; Segmented is a bordered button row.
/// </summary>
public enum TabsVariant
{
    /// <summary>Underlines the active tab, matching the report's top-level tab chrome.</summary>
    Underline,
    /// <summary>Renders tabs as pill buttons, used for the About sub-navigation.</summary>
    Pill,
    /// <summary>Renders tabs as a bordered button row.</summary>
    Segmented,
}
