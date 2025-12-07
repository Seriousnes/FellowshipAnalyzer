namespace FellowshipAnalyzer.Core.Common.Items;

/// <summary>
/// A static item definition used in item registries (the equivalent of WoWAnalyzer's ITEMS entries).
/// Contains identity and display metadata only.
/// </summary>
/// <remarks>
/// Item subtypes (e.g. trinkets, enchants, consumables) can be added later as sealed subrecords.
/// </remarks>
public sealed record Item(int Id, string Name, string Icon = "");

/// <summary>
/// Marker interface for item registry classes. Implement this on any class that declares static
/// <see cref="Item"/> properties to have them auto-discovered by <see cref="ItemRegistry"/>.
/// </summary>
public interface IItemRegistry
{
}

public class Items : IItemRegistry
{
    public static Item ShardOfTheExodar { get; } = new(1000101, "Shard of the Exodar", "T_Exodar_Shield.jpg");
    public static Item ShardOfTheVioletCitadel { get; } = new(1000102, "Shard of the Violet Citadel", "T_VioletCitadel_Shield.jpg");
    public static Item ShardOfTheSaroniteDefender { get; } = new(1000103, "Shard of the Saronite Defender", "T_SaroniteDefender_Shield.jpg");
}
