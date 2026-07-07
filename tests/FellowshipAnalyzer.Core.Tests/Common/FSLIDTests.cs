using System.Text.Json;
using FellowshipAnalyzer.Core.Common.Spells;
using Shouldly;
using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Common;

public class FSLIDTests
{
    [Theory]
    [InlineData(155, SpellKind.Ability, 155)]
    [InlineData(1_001_396, SpellKind.Effect, 1_396)]
    [InlineData(2_000_042, SpellKind.Talent, 42)]
    [InlineData(3_000_007, SpellKind.Weapon, 7)]
    public void Decodes_Kind_And_NativeId(int value, SpellKind kind, int nativeId)
    {
        var fslid = new FSLID(value);
        fslid.Kind.ShouldBe(kind);
        fslid.NativeId.ShouldBe(nativeId);
    }

    [Theory]
    [InlineData(SpellKind.Ability, 155, 155)]
    [InlineData(SpellKind.Effect, 1_396, 1_001_396)]
    [InlineData(SpellKind.Talent, 42, 2_000_042)]
    [InlineData(SpellKind.Weapon, 7, 3_000_007)]
    public void FromNative_Encodes_Value(SpellKind kind, int nativeId, int expected)
    {
        FSLID.FromNative(kind, nativeId).Value.ShouldBe(expected);
    }

    [Fact]
    public void Implicit_Conversions_Roundtrip_And_Compare()
    {
        FSLID fromInt = 1_001_396;
        int toInt = fromInt;
        toInt.ShouldBe(1_001_396);

        FSLID a = 42;
        FSLID b = 42;
        (a == b).ShouldBeTrue();
        (a == 42).ShouldBeTrue();
        a.Equals(b).ShouldBeTrue();
        a.GetHashCode().ShouldBe(b.GetHashCode());
    }

    [Fact]
    public void Converter_Reads_Number_As_Namespaced()
    {
        var fslid = JsonSerializer.Deserialize<FSLID>("1001396");
        fslid.Value.ShouldBe(1_001_396);
        fslid.Kind.ShouldBe(SpellKind.Effect);
    }

    [Theory]
    [InlineData("effect", 1_001_396)]
    [InlineData("talent", 2_000_042)]
    [InlineData("weapon", 3_000_007)]
    [InlineData("ability", 155)]
    public void Converter_Reads_Native_Object(string kind, int expectedValue)
    {
        var nativeId = kind switch { "effect" => 1_396, "talent" => 42, "weapon" => 7, _ => 155 };
        var json = $"{{ \"id\": {nativeId}, \"kind\": \"{kind}\" }}";
        JsonSerializer.Deserialize<FSLID>(json).Value.ShouldBe(expectedValue);
    }

    [Fact]
    public void Converter_Writes_Namespaced_Number()
    {
        JsonSerializer.Serialize(FSLID.FromNative(SpellKind.Effect, 1_396)).ShouldBe("1001396");
    }
}
