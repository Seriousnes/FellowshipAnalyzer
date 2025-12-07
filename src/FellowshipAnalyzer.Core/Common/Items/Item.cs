namespace FellowshipAnalyzer.Core.Common.Items;

/// <summary>
/// A static item definition used in item registries. Contains identity and display metadata only.
/// </summary>
/// <remarks>
/// Item subtypes (e.g. trinkets, enchants, consumables) can be added later as sealed subrecords.
/// </remarks>
public sealed record Item(int Id, string Name, string Icon = "");