namespace FellowshipAnalyzer.Core.UI.Components;

/// <summary>
/// One entry in a <see cref="Breadcrumbs"/> list. Items without a <see cref="Href"/>
/// are rendered as the non-navigable current page indicator.
/// </summary>
public sealed record BreadcrumbItem(string Label, string? Href = null);
