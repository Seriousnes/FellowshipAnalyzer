using FellowshipAnalyzer.Core.Analysis;

using Xunit;

namespace FellowshipAnalyzer.Core.Tests.Analysis;

public class CooldownStateTests
{
    private static CooldownState TwoSpent(double length = 20000)
    {
        var state = new CooldownState(2);
        state.Spend(0, length, 1.0);
        state.Spend(0, length, 1.0);
        return state;
    }

    [Fact]
    public void NewState()
    {
        var state = new CooldownState(2);

        Assert.Equal(2, state.Charges);
        Assert.False(state.Recharging);
        Assert.Null(state.NextChargeAt);
    }

    [Fact]
    public void MaximumFloor()
    {
        Assert.Equal(1, new CooldownState(0).MaxCharges);
    }

    [Fact]
    public void SingleCharge()
    {
        var state = new CooldownState(1);
        state.Spend(0, 30000, 1.0);

        Assert.Equal(0, state.Charges);
        Assert.Equal(30000, state.NextChargeAt);

        Assert.Equal(0, state.Advance(29999));
        Assert.Equal(1, state.ProgressLeft, precision: 6);
        Assert.Equal(0, state.Charges);

        Assert.Equal(1, state.Advance(30000));
        Assert.Equal(1, state.Charges);
    }

    [Fact]
    public void RateChangeKeepsProgress()
    {
        var state = new CooldownState(1);
        state.Spend(0, 30000, 1.0);
        state.SetRate(10000, 8.1);

        Assert.Equal(20000, state.ProgressLeft, precision: 6);
        Assert.Equal(12470, state.NextChargeAt);
        Assert.Equal(1, state.Advance(12470));
    }

    [Fact]
    public void RepeatedEqualRate()
    {
        var state = new CooldownState(1);
        state.Spend(0, 10000, 1.3);
        state.SetRate(1000, 1.3);
        state.SetRate(2000, 1.3);
        state.SetRate(3000, 1.3);

        Assert.Equal(6100, state.ProgressLeft, precision: 6);
    }

    [Fact]
    public void ChainOfTwoCharges()
    {
        var state = new CooldownState(2);
        state.Spend(0, 12000, 1.3);
        state.Spend(1000, 12000, 1.3);

        Assert.Equal(9231, state.NextChargeAt);

        Assert.Equal(1, state.Advance(9231));
        Assert.Equal(18462, state.NextChargeAt);

        Assert.Equal(1, state.Advance(18462));
        Assert.Equal(2, state.Charges);
    }

    [Fact]
    public void FixedRechargeLength()
    {
        var state = new CooldownState(2);
        state.Spend(0, 20000, 1.0);
        state.Spend(5000, 10000, 1.0);

        Assert.Equal(20000, state.RechargeLength, precision: 6);

        state.Advance(20000);

        Assert.Equal(1, state.Charges);
        Assert.Equal(20000, state.ProgressLeft, precision: 6);
        Assert.Equal(40000, state.NextChargeAt);
    }

    [Fact]
    public void SeveralChargesInOneAdvance()
    {
        var state = new CooldownState(3);
        state.Spend(0, 1000, 1.0);
        state.Spend(0, 1000, 1.0);
        state.Spend(0, 1000, 1.0);

        Assert.Equal(2, state.Advance(2500));
        Assert.Equal(2, state.Charges);
        Assert.Equal(500, state.ProgressLeft, precision: 6);
    }

    [Fact]
    public void EarlierTimestamp()
    {
        var state = new CooldownState(1);
        state.Spend(1000, 5000, 1.0);

        Assert.Equal(0, state.Advance(500));
        Assert.Equal(1000, state.Timestamp);
        Assert.Equal(5000, state.ProgressLeft, precision: 6);
    }

    [Fact]
    public void SpendWithNoCharge()
    {
        var state = new CooldownState(1);
        state.Spend(0, 5000, 1.0);

        Assert.False(state.Spend(100, 5000, 1.0));
        Assert.Equal(4900, state.ProgressLeft, precision: 6);
    }

    [Fact]
    public void ReduceCarriesToTheNextCharge()
    {
        var state = TwoSpent();

        Assert.Equal(1000, state.Reduce(19500, 1000), precision: 6);
        Assert.Equal(1, state.Charges);
        Assert.Equal(19500, state.ProgressLeft, precision: 6);
    }

    [Fact]
    public void ReduceEqualToTheProgressLeft()
    {
        var state = TwoSpent();

        Assert.Equal(500, state.Reduce(19500, 500), precision: 6);
        Assert.Equal(1, state.Charges);
        Assert.Equal(20000, state.ProgressLeft, precision: 6);
    }

    [Fact]
    public void ReducePastTheLastCharge()
    {
        var state = TwoSpent();

        Assert.Equal(20500, state.Reduce(19500, 25000), precision: 6);
        Assert.Equal(2, state.Charges);
        Assert.False(state.Recharging);
    }

    [Fact]
    public void ReduceOnAFullState()
    {
        var state = new CooldownState(1);

        Assert.Equal(0, state.Reduce(0, 5000), precision: 6);
    }

    [Fact]
    public void ReduceInCooldownMilliseconds()
    {
        var state = new CooldownState(1);
        state.Spend(0, 30000, 2.0);

        Assert.Equal(15000, state.NextChargeAt);

        state.Reduce(0, 10000);

        Assert.Equal(10000, state.NextChargeAt);
    }

    [Fact]
    public void GrantKeepsTheRecharge()
    {
        var state = TwoSpent();

        Assert.True(state.GrantCharge(5000));
        Assert.Equal(1, state.Charges);
        Assert.Equal(15000, state.ProgressLeft, precision: 6);

        Assert.True(state.GrantCharge(5000));
        Assert.Equal(2, state.Charges);
        Assert.Equal(0, state.ProgressLeft, precision: 6);
    }

    [Fact]
    public void CompleteRestartsTheNextCharge()
    {
        var state = TwoSpent();

        Assert.True(state.CompleteCharge(5000));
        Assert.Equal(1, state.Charges);
        Assert.Equal(20000, state.ProgressLeft, precision: 6);
        Assert.Equal(5000, state.ChargeStart);
    }

    [Fact]
    public void Held()
    {
        var state = new CooldownState(1);
        state.Spend(0, 60000, 1.0, hold: true);
        state.Advance(30000);

        Assert.True(state.Held);
        Assert.Equal(60000, state.ProgressLeft, precision: 6);
        Assert.Null(state.NextChargeAt);

        state.Release(30000, 60000, 1.5);

        Assert.Equal(70000, state.NextChargeAt);
        Assert.Equal(30000, state.OverallStart);
    }

    [Fact]
    public void Reset()
    {
        var state = TwoSpent();

        state.ResetAll(100);

        Assert.Equal(2, state.Charges);
        Assert.Equal(0, state.ProgressLeft, precision: 6);
        Assert.Null(state.NextChargeAt);
    }

    [Fact]
    public void RateOfZero()
    {
        var state = new CooldownState(1);
        state.Spend(0, 5000, 0.0);
        state.Advance(10000);

        Assert.Equal(5000, state.ProgressLeft, precision: 6);
        Assert.Null(state.NextChargeAt);
    }
}
