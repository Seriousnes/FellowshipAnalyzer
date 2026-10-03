using FellowshipAnalyzer.Core.Analysis.Normalizers;
using FellowshipAnalyzer.Core.Events;
using FellowshipAnalyzer.Core.Game;

using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Analysis.Normalizers;

public sealed class ResourceChangeNormalizerTests
{
    private const int PlayerId = 1;
    private const int TankId = 2;
    private const int BossId = 50;
    private const int Strike = 2262;
    private const int Mend = 2294;

    [Fact]
    public void Normalize_AUnitsFirstReading_IsAChangeFromZeroWithNoPreviousAmount()
    {
        var damage = Damage(1_000, Strike, Block(ResourceTypes.Primary, 40, 100));

        var result = Run(damage);

        var change = Assert.IsType<ResourceChangeEvent>(result[0]);
        Assert.Same(damage, result[1]);
        Assert.Equal(40, change.ResourceChange);
        Assert.Equal(40, change.ResourceAmount);
        Assert.Equal(100, change.ResourceMax);
        Assert.Null(change.PreviousResourceAmount);
        Assert.Same(damage, change.Trigger);
        Assert.True(change.Fabricated);
    }

    [Fact]
    public void Normalize_AnUnchangedResource_RaisesNoFurtherChange()
    {
        var first = Damage(1_000, Strike, Block(ResourceTypes.Primary, 40, 100));
        var second = Damage(1_500, Strike, Block(ResourceTypes.Primary, 40, 100));

        var result = Run(first, second);

        Assert.Single(result.OfType<ResourceChangeEvent>());
    }

    [Fact]
    public void Normalize_AChangedResource_IsPlacedBeforeTheEventThatRevealedIt()
    {
        var first = Damage(1_000, Strike, Block(ResourceTypes.Primary, 40, 100));
        var second = Damage(1_500, Strike, Block(ResourceTypes.Primary, 25, 100));

        var result = Run(first, second);

        var change = Assert.IsType<ResourceChangeEvent>(result[2]);
        Assert.Same(second, result[3]);
        Assert.Equal(-15, change.ResourceChange);
        Assert.Equal(40, change.PreviousResourceAmount);
        Assert.Equal(25, change.ResourceAmount);
        Assert.Equal(1_500, change.Timestamp);
    }

    [Fact]
    public void Normalize_EveryResourceInABlock_RaisesItsOwnChange()
    {
        var damage = Damage(
            1_000,
            Strike,
            new ActorResources
            {
                Resources =
                [
                    new ClassResource { Type = ResourceTypes.Mana, Amount = 500, Max = 1_000 },
                    new ClassResource { Type = ResourceTypes.Primary, Amount = 40, Max = 100 },
                ],
            });

        var result = Run(damage);

        Assert.Equal(
            [ResourceTypes.Mana, ResourceTypes.Primary],
            result.OfType<ResourceChangeEvent>().Select(change => change.ResourceChangeType));
    }

    [Fact]
    public void Normalize_AChangeOnTheTargetsBlock_NamesTheTargetAsTheUnitWhoseResourceChanged()
    {
        var resources = Block(ResourceTypes.Stagger, 5_000, -1);
        var heal = new HealEvent
        {
            Timestamp = 1_000,
            SourceId = PlayerId,
            TargetId = TankId,
            Ability = new Ability { Id = Mend },
            TargetResources = resources,
        };

        var change = Assert.Single(Run(heal).OfType<ResourceChangeEvent>());

        Assert.Equal(TankId, change.SourceId);
        Assert.Equal(TankId, change.TargetId);
        Assert.Equal(Mend, change.Ability.Id);
        Assert.Same(resources, change.UnitResources);
    }

    [Fact]
    public void Normalize_EachUnitsResources_AreComparedOnlyWithItsOwn()
    {
        var player = Damage(1_000, Strike, Block(ResourceTypes.Stagger, 300, -1));
        var tank = new HealEvent
        {
            Timestamp = 1_100,
            SourceId = PlayerId,
            TargetId = TankId,
            Ability = new Ability { Id = Mend },
            TargetResources = Block(ResourceTypes.Stagger, 300, -1),
        };

        var changes = Run(player, tank).OfType<ResourceChangeEvent>().ToList();

        Assert.Equal([PlayerId, TankId], changes.Select(change => change.SourceId));
        Assert.All(changes, change => Assert.Null(change.PreviousResourceAmount));
    }

    [Fact]
    public void Normalize_AChangeACastsBlockRevealed_NamesNoAbility()
    {
        var cast = new CastEvent
        {
            Timestamp = 1_000,
            SourceId = PlayerId,
            TargetId = BossId,
            Ability = new Ability { Id = Strike },
            SourceResources = Block(ResourceTypes.Primary, 40, 100),
        };

        var change = Assert.Single(Run(cast).OfType<ResourceChangeEvent>());

        Assert.Same(Ability.UnknownAbility, change.Ability);
    }

    [Fact]
    public void Normalize_AChangeInMaximumAlone_RaisesAChangeOfZero()
    {
        var first = Damage(1_000, Strike, Block(ResourceTypes.Primary, 40, 100));
        var second = Damage(1_500, Strike, Block(ResourceTypes.Primary, 40, 120));

        var change = Run(first, second).OfType<ResourceChangeEvent>().Last();

        Assert.Equal(0, change.ResourceChange);
        Assert.Equal(120, change.ResourceMax);
    }

    private static List<Event> Run(params Event[] events) =>
        new ResourceChangeNormalizer().Normalize([.. events], PlayerId);

    private static ActorResources Block(ResourceTypes type, int amount, int max) => new()
    {
        HitPoints = 10_000,
        MaxHitPoints = 20_000,
        Resources = [new ClassResource { Type = type, Amount = amount, Max = max }],
    };

    private static DamageEvent Damage(int timestamp, int abilityId, ActorResources sourceResources) => new()
    {
        Timestamp = timestamp,
        SourceId = PlayerId,
        TargetId = BossId,
        Ability = new Ability { Id = abilityId },
        SourceResources = sourceResources,
    };
}
