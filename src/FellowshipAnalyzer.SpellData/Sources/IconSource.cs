using System.Text.Json;

using Fellowship.SDK.Documents;

using FellowshipAnalyzer.Core.Common.Spells;

namespace FellowshipAnalyzer.SpellData.Sources;

/// <summary>Icon lookups by kind and native id, sourced from the export's <c>entities.jsonl</c>.</summary>
public sealed class IconSource
{
    private static readonly string[] Rungs =
        ["common", "uncommon", "rare", "epic", "champion", "heroic", "legendary"];

    private readonly Dictionary<(SpellKind Kind, int Id), string> _icons;

    private IconSource(Dictionary<(SpellKind, int), string> icons, SortedSet<string> artSharedAcrossRungs)
    {
        _icons = icons;
        ArtSharedAcrossRungs = artSharedAcrossRungs;
    }

    /// <summary>
    /// Item and gem art the export draws once for every rarity rung, named without directory or extension.
    /// An item drawn per rung declares an <c>iconByRarity</c> map; one that declares none, and every gem,
    /// draws a single file every rung shares. Art named here is addressed by its bare name at any tier;
    /// art absent from it ends in the rung's stored name.
    /// </summary>
    public SortedSet<string> ArtSharedAcrossRungs { get; }

    /// <summary>Returns the icon filename the build declares for the entity, or <c>null</c> if it declares none.</summary>
    public string? IconFor(SpellKind kind, int nativeId) =>
        _icons.TryGetValue((kind, nativeId), out var icon) ? icon : null;

    public static IconSource Load(string entitiesPath)
    {
        var icons = new Dictionary<(SpellKind, int), string>();
        var shared = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var line in File.ReadLines(entitiesPath))
        {
            if (line.Length == 0)
                continue;

            var document = JsonSerializer.Deserialize<EntityDocument>(line, EntityDocuments.Options);

            switch (document)
            {
                case ItemDocument { IconByRarity: null or { Count: 0 }, Icon: { Length: > 0 } art }:
                    shared.Add(ArtName(art));
                    continue;
                case GemDocument { Icon: { Length: > 0 } gem }:
                    shared.Add(ArtName(gem));
                    continue;
            }

            var (kind, icon) = document switch
            {
                AbilityDocument ability => (SpellKind.Ability, ability.Icon),
                EffectDocument effect => (SpellKind.Effect, effect.Icon),
                TalentDocument talent => (SpellKind.Talent, talent.Icon),
                TraitDocument trait => (SpellKind.Weapon, trait.Icon),
                _ => default((SpellKind, string?)),
            };

            if (document is not null && icon is { Length: > 0 })
                icons[(kind, document.Id)] = icon;
        }

        return new IconSource(icons, shared);
    }

    private static string ArtName(string art)
    {
        var name = Path.GetFileNameWithoutExtension(art);

        foreach (var rung in Rungs)
            if (name.EndsWith('-' + rung, StringComparison.Ordinal))
                return name[..^(rung.Length + 1)];

        return name;
    }
}
