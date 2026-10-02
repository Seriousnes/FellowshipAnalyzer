using System.Collections.Frozen;

using FellowshipAnalyzer.Core.Analysis.Gems;
using FellowshipAnalyzer.Core.Common.Items;
using FellowshipAnalyzer.Core.Events;

namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Resolves the stat effects a player brings into the dungeon that no apply or remove event marks, and hands
/// them to <see cref="StatTracker"/>: the gem traits their gem power unlocks, the set bonuses their equipped
/// pieces complete, and the dungeon's modifiers. An Anomalous Residue adds gem power for as long as it lasts,
/// so the gem traits are resolved again whenever one is gained, stacked, or lost.
/// </summary>
/// <remarks>
/// Magnitudes are the codex effect records each gem trait, set bonus, and modifier grants. A gem trait that
/// adds a rating, and Blessing of the Commander's Ability Cooldown Reduction, are already counted at the
/// player's own gem power, in the combatantinfo totals and in <see cref="CombatantStats"/> respectively, so
/// only the change an Anomalous Residue makes to them is added. A flat percentage is counted in neither, so
/// it is added in full.
/// </remarks>
[Dependency<StatTracker>]
public sealed partial class PassiveBonusTracker : Analyzer
{
    /// <summary>Gem power each stack of an Anomalous Residue adds to its gem.</summary>
    public const int AnomalousResiduePower = 480;

    /// <summary>The Challenge dungeon modifier, which grants +10% Spirit.</summary>
    public const int ChallengeModifier = 10;

    /// <summary>The Ultimatum dungeon modifier, which reduces every ability's cooldown by 10%.</summary>
    public const int UltimatumModifier = 16;

    private const double CommanderReduction = 0.04;
    private const double CommanderReductionII = 0.12;

    /// <summary>Blessing of the Commander: Ability Cooldown Reduction at rank 5, replaced by a larger one at rank 10.</summary>
    public static readonly GemTrait BlessingOfTheCommander = new(
        Items.BlessingOfTheCommander, GemRankPower.Rank5,
        Items.BlessingOfTheCommanderII, GemRankPower.Rank10);

    private static readonly PassiveGemTrait[] GemTraits =
    [
        new(GemType.Amethyst,
            new(Items.KillerInstinct, GemRankPower.Rank4, Items.KillerInstinctII, GemRankPower.Rank9),
            new() { Crit = 0.02 }, new() { Crit = 0.06 }, AlreadyCounted: false),
        new(GemType.Amethyst,
            new(Items.BerserkersZeal, GemRankPower.Rank2, Items.BerserkersZealII, GemRankPower.Rank7),
            new() { CritRating = 8 }, new() { CritRating = 24 }, AlreadyCounted: true),
        new(GemType.Topaz,
            new(Items.FelineGrace, GemRankPower.Rank4, Items.FelineGraceII, GemRankPower.Rank9),
            new() { Haste = 0.02 }, new() { Haste = 0.06 }, AlreadyCounted: false),
        new(GemType.Topaz,
            new(Items.ThiefsAlacrity, GemRankPower.Rank2, Items.ThiefsAlacrityII, GemRankPower.Rank7),
            new() { HasteRating = 8 }, new() { HasteRating = 24 }, AlreadyCounted: true),
        new(GemType.Emerald,
            new(Items.TacticiansAccumen, GemRankPower.Rank4, Items.TacticiansAccumenII, GemRankPower.Rank9),
            new() { Expertise = 0.02 }, new() { Expertise = 0.06 }, AlreadyCounted: false),
        new(GemType.Emerald,
            new(Items.VanguardsResolve, GemRankPower.Rank2, Items.VanguardsResolveII, GemRankPower.Rank7),
            new() { ExpertiseRating = 8 }, new() { ExpertiseRating = 24 }, AlreadyCounted: true),
        new(GemType.Emerald,
            BlessingOfTheCommander,
            new() { AbilityCooldownReduction = CommanderReduction },
            new() { AbilityCooldownReduction = CommanderReductionII }, AlreadyCounted: true),
        new(GemType.Sapphire,
            new(Items.OraclesForesight, GemRankPower.Rank4, Items.OraclesForesightII, GemRankPower.Rank9),
            new() { Spirit = 0.02 }, new() { Spirit = 0.06 }, AlreadyCounted: false),
        new(GemType.Sapphire,
            new(Items.MysticsIntuition, GemRankPower.Rank2, Items.MysticsIntuitionII, GemRankPower.Rank7),
            new() { SpiritRating = 8 }, new() { SpiritRating = 24 }, AlreadyCounted: true),
    ];

    private static readonly PassiveSetBonus[] SetBonuses =
    [
        new("Tuzari Grace", 2, new() { Haste = 0.04 }),
        new("Sin Warding", 2, new() { Expertise = 0.04 }),
        new("Sinthara's Veil", 2, new() { Spirit = 0.04 }),
        new("Death's Grasp", 2, new() { Spirit = 0.04 }),
        new("Haunting Lament", 2, new() { Spirit = 0.04 }),
        new("Draconic Deceit", 2, new() { Crit = 0.04 }),
        new("Draconic Fury", 2, new() { Crit = 0.04 }),
        new("Expedition Heroism", 10, new() { Crit = 0.15, Haste = 0.20, Expertise = 0.20, Spirit = 0.20 }),
    ];

    private static readonly FrozenDictionary<int, PassiveStatBonus> ModifierBonuses =
        new Dictionary<int, PassiveStatBonus>
        {
            [ChallengeModifier] = new() { Spirit = 0.10 },
            [UltimatumModifier] = new() { AbilityCooldownReduction = 0.10 },
        }.ToFrozenDictionary();

    private static readonly FrozenDictionary<int, GemType> AnomalousResidues =
        new Dictionary<int, GemType>
        {
            [Items.AnomalousResidueRuby.FSLID] = GemType.Ruby,
            [Items.AnomalousResidueAmethyst.FSLID] = GemType.Amethyst,
            [Items.AnomalousResidueTopaz.FSLID] = GemType.Topaz,
            [Items.AnomalousResidueEmerald.FSLID] = GemType.Emerald,
            [Items.AnomalousResidueSapphire.FSLID] = GemType.Sapphire,
            [Items.AnomalousResidueDiamond.FSLID] = GemType.Diamond,
        }.ToFrozenDictionary();

    private static readonly object GemSource = new();
    private static readonly object SetSource = new();
    private static readonly object ModifierSource = new();

    private readonly Dictionary<GemType, int> _residueStacks = [];

    /// <summary>The Ability Cooldown Reduction Blessing of the Commander grants at <paramref name="emeraldPower"/>.</summary>
    public static double BlessingOfTheCommanderReduction(int emeraldPower) =>
        BlessingOfTheCommander.ByRank(emeraldPower, CommanderReduction, CommanderReductionII, 0.0);

    /// <summary>The player's power in <paramref name="gem"/> right now: their own, plus every Anomalous Residue stack of that gem.</summary>
    public int CurrentGemPower(GemType gem) =>
        BaseGemPower(gem) + _residueStacks.GetValueOrDefault(gem) * AnomalousResiduePower;

    [On<DungeonStartEvent>]
    private void OnDungeonStart(DungeonStartEvent e)
    {
        StatTracker.SetPassiveBonus(SetSource, ResolveSetBonuses(), e);
        StatTracker.SetPassiveBonus(ModifierSource, ResolveModifierBonuses(), e);
        StatTracker.SetPassiveBonus(GemSource, ResolveGemBonuses(), e);
    }

    [On<ApplyBuffEvent>(To = Actor.Player)]
    private void OnApplyBuff(ApplyBuffEvent e) => SetResidue(e.Ability.Id, 1, e);

    [On<ApplyBuffStackEvent>(To = Actor.Player)]
    private void OnApplyBuffStack(ApplyBuffStackEvent e) => SetResidue(e.Ability.Id, e.Stack, e);

    [On<RemoveBuffStackEvent>(To = Actor.Player)]
    private void OnRemoveBuffStack(RemoveBuffStackEvent e) => SetResidue(e.Ability.Id, e.Stack, e);

    [On<RemoveBuffEvent>(To = Actor.Player)]
    private void OnRemoveBuff(RemoveBuffEvent e) => SetResidue(e.Ability.Id, 0, e);

    private void SetResidue(int spellId, int stacks, Event trigger)
    {
        if (!AnomalousResidues.TryGetValue(spellId, out var gem)) return;
        if (_residueStacks.GetValueOrDefault(gem) == stacks) return;

        _residueStacks[gem] = stacks;
        StatTracker.SetPassiveBonus(GemSource, ResolveGemBonuses(), trigger);
    }

    private PassiveStatBonus ResolveGemBonuses()
    {
        var total = PassiveStatBonus.None;
        foreach (var trait in GemTraits)
        {
            total += trait.At(CurrentGemPower(trait.Gem));
            if (trait.AlreadyCounted) total -= trait.At(BaseGemPower(trait.Gem));
        }
        return total;
    }

    private PassiveStatBonus ResolveSetBonuses()
    {
        var pieces = new Dictionary<string, int>();
        foreach (var item in Owner.SelectedCombatant.Gear)
        {
            if (item.Set?.Name is { Length: > 0 } name)
                pieces[name] = pieces.GetValueOrDefault(name) + 1;
        }

        var total = PassiveStatBonus.None;
        foreach (var set in SetBonuses)
        {
            if (pieces.GetValueOrDefault(set.Name) >= set.Pieces)
                total += set.Bonus;
        }
        return total;
    }

    private PassiveStatBonus ResolveModifierBonuses()
    {
        var total = PassiveStatBonus.None;
        foreach (var modifier in Owner.Dungeon.Modifiers ?? [])
        {
            if (ModifierBonuses.TryGetValue(modifier, out var bonus))
                total += bonus;
        }
        return total;
    }

    private int BaseGemPower(GemType gem)
    {
        var combatant = Owner.SelectedCombatant;
        return gem switch
        {
            GemType.Ruby => combatant.Ruby,
            GemType.Amethyst => combatant.Amethyst,
            GemType.Topaz => combatant.Topaz,
            GemType.Emerald => combatant.Emerald,
            GemType.Sapphire => combatant.Sapphire,
            GemType.Diamond => combatant.Diamond,
            _ => 0,
        };
    }

    private sealed record PassiveGemTrait(
        GemType Gem,
        GemTrait Trait,
        PassiveStatBonus Based,
        PassiveStatBonus Upgraded,
        bool AlreadyCounted)
    {
        public PassiveStatBonus At(int gemPower) => Trait.ByRank(gemPower, Based, Upgraded, PassiveStatBonus.None);
    }

    private sealed record PassiveSetBonus(string Name, int Pieces, PassiveStatBonus Bonus);
}
