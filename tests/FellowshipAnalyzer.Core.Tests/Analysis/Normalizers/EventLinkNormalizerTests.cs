using FellowshipAnalyzer.Core.Analysis.Normalizers;
using FellowshipAnalyzer.Core.Common.Spells;
using FellowshipAnalyzer.Core.Events;

using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Analysis.Normalizers;

public sealed class EventLinkNormalizerTests
{
    private const string Relation = "CastDamage";
    private const int PlayerId = 1;
    private const int GrimCarve = 2262;
    private const int HeartSplitter = 2294;
    private const int BossId = 50;

    [Fact]
    public void Normalize_DamageInsideTheForwardBuffer_IsLinkedToTheCast()
    {
        var cast = Cast(GrimCarve, 1_000);
        var first = Damage(GrimCarve, 1_100, amount: 10);
        var second = Damage(GrimCarve, 1_600, amount: 20);

        Run([cast, first, second], Link(forwardBufferMs: 1_000));

        Assert.Equal([first, second], cast.RelatedEvents<DamageEvent>(Relation));
    }

    [Fact]
    public void Normalize_DamageBeyondTheForwardBuffer_IsNotLinked()
    {
        var cast = Cast(GrimCarve, 1_000);
        var late = Damage(GrimCarve, 2_001);

        Run([cast, late], Link(forwardBufferMs: 1_000));

        Assert.Empty(cast.RelatedEvents<DamageEvent>(Relation));
    }

    [Fact]
    public void Normalize_NoBuffer_LinksOnlyWithinTheSameTimestamp()
    {
        var cast = Cast(GrimCarve, 1_000);
        var same = Damage(GrimCarve, 1_000);
        var next = Damage(GrimCarve, 1_001);

        Run([cast, same, next], Link());

        Assert.Equal([same], cast.RelatedEvents<DamageEvent>(Relation));
    }

    [Fact]
    public void Normalize_DamageInsideTheBackwardBuffer_IsLinkedToTheCast()
    {
        var early = Damage(GrimCarve, 900);
        var cast = Cast(GrimCarve, 1_000);

        Run([early, cast], Link(backwardBufferMs: 200));

        Assert.Equal([early], cast.RelatedEvents<DamageEvent>(Relation));
    }

    [Fact]
    public void Normalize_ADifferentAbility_IsNotLinked()
    {
        var cast = Cast(GrimCarve, 1_000);
        var other = Damage(HeartSplitter, 1_100);

        Run([cast, other], Link(forwardBufferMs: 1_000));

        Assert.Empty(cast.RelatedEvents<DamageEvent>(Relation));
    }

    [Fact]
    public void Normalize_NoReferencedAbilities_LinksAnyAbility()
    {
        var cast = Cast(GrimCarve, 1_000);
        var other = Damage(HeartSplitter, 1_100);

        Run([cast, other], Link(forwardBufferMs: 1_000) with { ReferencedAbilityIds = null });

        Assert.Equal([other], cast.RelatedEvents<DamageEvent>(Relation));
    }

    [Fact]
    public void Normalize_ADifferentSource_IsNotLinked()
    {
        var cast = Cast(GrimCarve, 1_000);
        var other = Damage(GrimCarve, 1_100, sourceId: PlayerId + 1);

        Run([cast, other], Link(forwardBufferMs: 1_000));

        Assert.Empty(cast.RelatedEvents<DamageEvent>(Relation));
    }

    [Fact]
    public void Normalize_AnySource_LinksAcrossSources()
    {
        var cast = Cast(GrimCarve, 1_000);
        var other = Damage(GrimCarve, 1_100, sourceId: PlayerId + 1);

        Run([cast, other], Link(forwardBufferMs: 1_000) with { AnySource = true });

        Assert.Equal([other], cast.RelatedEvents<DamageEvent>(Relation));
    }

    [Fact]
    public void Normalize_ADifferentTarget_IsNotLinkedWithoutAnyTarget()
    {
        var cast = Cast(GrimCarve, 1_000);
        var other = Damage(GrimCarve, 1_100, targetId: BossId + 1);

        Run([cast, other], Link(forwardBufferMs: 1_000) with { AnyTarget = false });

        Assert.Empty(cast.RelatedEvents<DamageEvent>(Relation));
    }

    [Fact]
    public void Normalize_ADifferentTargetInstance_IsNotLinkedWithoutAnyTarget()
    {
        var cast = Cast(GrimCarve, 1_000);
        var other = Damage(GrimCarve, 1_100, targetInstance: 2);

        Run([cast, other], Link(forwardBufferMs: 1_000) with { AnyTarget = false });

        Assert.Empty(cast.RelatedEvents<DamageEvent>(Relation));
    }

    [Fact]
    public void Normalize_ASecondCastOfTheSameAbility_EndsTheFirstCastsWindow()
    {
        var first = Cast(GrimCarve, 1_000);
        var firstDamage = Damage(GrimCarve, 1_100);
        var second = Cast(GrimCarve, 1_500);
        var secondDamage = Damage(GrimCarve, 1_600);

        Run([first, firstDamage, second, secondDamage], Link(forwardBufferMs: 5_000));

        Assert.Equal([firstDamage], first.RelatedEvents<DamageEvent>(Relation));
        Assert.Equal([secondDamage], second.RelatedEvents<DamageEvent>(Relation));
    }

    [Fact]
    public void Normalize_DamageSharingTheNextCastsTimestamp_IsClaimedByOnlyTheFirstCast()
    {
        var first = Cast(GrimCarve, 1_000);
        var damage = Damage(GrimCarve, 3_000, amount: 10);
        var second = Cast(GrimCarve, 3_000);

        Run([first, damage, second], Link(forwardBufferMs: 2_500));

        Assert.Equal([damage], first.RelatedEvents<DamageEvent>(Relation));
        Assert.Empty(second.RelatedEvents<DamageEvent>(Relation));
    }

    [Fact]
    public void Normalize_DamageBetweenTwoCastsWithABackwardBuffer_IsClaimedByOnlyTheFirstCast()
    {
        var first = Cast(GrimCarve, 1_000);
        var damage = Damage(GrimCarve, 1_800, amount: 10);
        var second = Cast(GrimCarve, 2_400);

        Run([first, damage, second], Link(forwardBufferMs: 2_500, backwardBufferMs: 1_000));

        Assert.Equal([damage], first.RelatedEvents<DamageEvent>(Relation));
        Assert.Empty(second.RelatedEvents<DamageEvent>(Relation));
    }

    [Fact]
    public void Normalize_ASecondCastOnAnotherTarget_DoesNotEndATargetScopedWindow()
    {
        var cast = Cast(GrimCarve, 1_000);
        var otherCast = Cast(GrimCarve, 1_200, targetId: BossId + 1);
        var damage = Damage(GrimCarve, 1_400);

        Run([cast, otherCast, damage], Link(forwardBufferMs: 5_000) with { AnyTarget = false });

        Assert.Equal([damage], cast.RelatedEvents<DamageEvent>(Relation));
    }

    [Fact]
    public void Normalize_ASecondCastOnTheSameTarget_EndsATargetScopedWindow()
    {
        var first = Cast(GrimCarve, 1_000);
        var second = Cast(GrimCarve, 1_200);
        var damage = Damage(GrimCarve, 1_400);

        Run([first, second, damage], Link(forwardBufferMs: 5_000) with { AnyTarget = false });

        Assert.Empty(first.RelatedEvents<DamageEvent>(Relation));
        Assert.Equal([damage], second.RelatedEvents<DamageEvent>(Relation));
    }

    [Fact]
    public void Normalize_ASecondCastFromAnotherSource_DoesNotEndTheWindow()
    {
        var cast = Cast(GrimCarve, 1_000);
        var otherCast = Cast(GrimCarve, 1_200, sourceId: PlayerId + 1);
        var damage = Damage(GrimCarve, 1_400);

        Run([cast, otherCast, damage], Link(forwardBufferMs: 5_000));

        Assert.Equal([damage], cast.RelatedEvents<DamageEvent>(Relation));
    }

    [Fact]
    public void Normalize_ADerivedReferencedEventType_MatchesTheBaseTypeNamed()
    {
        var cast = Cast(GrimCarve, 1_000);
        var applied = new ApplyBuffEvent
        {
            Timestamp = 1_100,
            SourceId = PlayerId,
            TargetId = BossId,
            Ability = new Ability { Id = GrimCarve },
        };

        Run([cast, applied], Link(forwardBufferMs: 1_000) with { ReferencedEventType = typeof(BuffEvent) });

        Assert.Equal([applied], cast.RelatedEvents<BuffEvent>(Relation));
    }

    [Fact]
    public void Normalize_AnotherRelation_IsNotReturned()
    {
        var cast = Cast(HeartSplitter, 1_000);
        var direct = Damage(HeartSplitter, 1_010);
        var exsanguinate = Damage(GrimCarve, 1_010);

        Run(
            [cast, direct, exsanguinate],
            Link(forwardBufferMs: 1_000) with
            {
                LinkingAbilityIds = [new FSLID(HeartSplitter)],
                ReferencedAbilityIds = [new FSLID(HeartSplitter)],
            },
            Link(forwardBufferMs: 1_000) with
            {
                Relation = "Exsanguinate",
                LinkingAbilityIds = [new FSLID(HeartSplitter)],
                ReferencedAbilityIds = [new FSLID(GrimCarve)],
            });

        Assert.Equal([direct], cast.RelatedEvents<DamageEvent>(Relation));
        Assert.Equal([exsanguinate], cast.RelatedEvents<DamageEvent>("Exsanguinate"));
        Assert.Same(direct, cast.RelatedEvent<DamageEvent>(Relation));
    }

    [Fact]
    public void Normalize_AnUnlinkedEvent_HasNoLinkedEvents()
    {
        var damage = Damage(GrimCarve, 1_000);

        Run([damage], Link(forwardBufferMs: 1_000));

        Assert.Empty(damage.LinkedEvents);
    }

    [Fact]
    public void LinkedEvents_AddedToDirectly_IsReadBackByRelatedEvents()
    {
        var cast = Cast(GrimCarve, 1_000);
        var damage = Damage(GrimCarve, 1_100);

        cast.LinkedEvents.Add(new LinkedEvent(damage, Relation));

        Assert.Equal([damage], cast.RelatedEvents<DamageEvent>(Relation));
    }

    [Fact]
    public void Normalize_RunTwiceOverTheSameEvents_DoesNotDoubleTheLinks()
    {
        var cast = Cast(GrimCarve, 1_000);
        var damage = Damage(GrimCarve, 1_100, amount: 10);
        var normalizer = new TestNormalizer([Link(forwardBufferMs: 1_000)]);

        normalizer.Normalize([cast, damage], PlayerId);
        normalizer.Normalize([cast, damage], PlayerId);

        Assert.Equal([damage], cast.RelatedEvents<DamageEvent>(Relation));
    }

    [Fact]
    public void Normalize_ReturnsEveryEventItWasGiven()
    {
        var cast = Cast(GrimCarve, 1_000);
        var damage = Damage(GrimCarve, 1_100);

        var result = new TestNormalizer([Link(forwardBufferMs: 1_000)]).Normalize([cast, damage], PlayerId);

        Assert.Equal([cast, damage], result);
    }

    private static void Run(List<Event> events, params EventLink[] links) =>
        new TestNormalizer([.. links]).Normalize(events, PlayerId);

    private static EventLink Link(int forwardBufferMs = 0, int backwardBufferMs = 0) => new()
    {
        Relation = Relation,
        LinkingEventType = typeof(CastEvent),
        LinkingAbilityIds = [new FSLID(GrimCarve)],
        ReferencedEventType = typeof(DamageEvent),
        ReferencedAbilityIds = [new FSLID(GrimCarve)],
        ForwardBufferMs = forwardBufferMs,
        BackwardBufferMs = backwardBufferMs,
        AnyTarget = true,
    };

    private static CastEvent Cast(int abilityId, int timestamp, int sourceId = PlayerId, int targetId = BossId) => new()
    {
        Timestamp = timestamp,
        SourceId = sourceId,
        TargetId = targetId,
        Ability = new Ability { Id = abilityId },
    };

    private static DamageEvent Damage(
        int abilityId,
        int timestamp,
        long amount = 0,
        int sourceId = PlayerId,
        int targetId = BossId,
        int? targetInstance = null) => new()
        {
            Timestamp = timestamp,
            SourceId = sourceId,
            TargetId = targetId,
            TargetInstance = targetInstance,
            Amount = amount,
            Ability = new Ability { Id = abilityId },
        };

    private sealed class TestNormalizer(List<EventLink> links) : EventLinkNormalizer(links);
}
