namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Charges of one ability and the cooldown progress left on the charge recharging. Progress is a
/// <see cref="double"/> in cooldown milliseconds and decreases at <see cref="Rate"/> cooldown milliseconds
/// per real millisecond. Every timestamp is an <see cref="int"/> of real milliseconds. Each call advances
/// the state from <see cref="Timestamp"/> to the call's timestamp at the rate in force across that span,
/// so a rate change leaves the progress already made unchanged. Charges recharge one at a time, and every
/// charge of a chain recharges over the <see cref="RechargeLength"/> of the spend from
/// <see cref="MaxCharges"/> charges.
/// </summary>
public sealed class CooldownState
{
    private const double Tolerance = 1e-6;

    /// <summary>
    /// Creates a state with every charge available.
    /// </summary>
    /// <param name="maxCharges">Maximum charges. A value below 1 gives 1.</param>
    public CooldownState(int maxCharges)
    {
        MaxCharges = Math.Max(1, maxCharges);
        Charges = MaxCharges;
    }

    /// <summary>Maximum charges, fixed at construction.</summary>
    public int MaxCharges { get; }

    /// <summary>Charges available at <see cref="Timestamp"/>.</summary>
    public int Charges { get; private set; }

    /// <summary>A charge is recharging, which is <see cref="Charges"/> below <see cref="MaxCharges"/>.</summary>
    public bool Recharging => Charges < MaxCharges;

    /// <summary>Progress is paused from a <see cref="Spend"/> with <c>hold</c> until <see cref="Release"/>, <see cref="CompleteCharge"/> or <see cref="ResetAll"/>.</summary>
    public bool Held { get; private set; }

    /// <summary>Cooldown milliseconds left on the charge recharging. 0 when <see cref="Recharging"/> is <see langword="false"/>.</summary>
    public double ProgressLeft { get; private set; }

    /// <summary>Cooldown milliseconds of one full recharge.</summary>
    public double RechargeLength { get; private set; }

    /// <summary>Recovery rate in cooldown milliseconds per real millisecond. 1.0 is real time.</summary>
    public double Rate { get; private set; } = 1.0;

    /// <summary>Real millisecond the state was last advanced to.</summary>
    public int Timestamp { get; private set; }

    /// <summary>Timestamp of the last spend from <see cref="MaxCharges"/> charges, or of the last <see cref="Release"/>.</summary>
    public int OverallStart { get; private set; }

    /// <summary>Timestamp at which the charge recharging started its recharge.</summary>
    public int ChargeStart { get; private set; }

    /// <summary>
    /// Real millisecond the charge recharging returns at the current <see cref="Rate"/>. <see langword="null"/>
    /// when no charge is recharging, when the state is <see cref="Held"/>, or when the rate is not positive.
    /// </summary>
    public int? NextChargeAt
    {
        get
        {
            if (!Recharging || Held || Rate <= 0)
            {
                return null;
            }

            return Timestamp + Math.Max(0, (int)Math.Ceiling(ProgressLeft / Rate - Tolerance));
        }
    }

    /// <summary>Advances the state to <paramref name="now"/>. Returns the charges returned by the call.</summary>
    public int Advance(int now)
    {
        if (now <= Timestamp)
        {
            return 0;
        }

        if (!Recharging || Held || Rate <= 0)
        {
            Timestamp = now;
            return 0;
        }

        var span = (double)(now - Timestamp);
        var returned = 0;
        while (Recharging)
        {
            var needed = ProgressLeft / Rate;
            if (needed > span + Tolerance)
            {
                ProgressLeft -= span * Rate;
                break;
            }

            span = Math.Max(0, span - needed);
            Charges++;
            returned++;
            ChargeStart = now - (int)Math.Floor(span);
            ProgressLeft = Recharging ? RechargeLength : 0;
        }

        Timestamp = now;
        return returned;
    }

    /// <summary>
    /// Advances to <paramref name="now"/> at the earlier rate, then applies <paramref name="rate"/> to the span
    /// after <paramref name="now"/>. Returns the charges returned by the advance.
    /// </summary>
    public int SetRate(int now, double rate)
    {
        var returned = Advance(now);
        Rate = rate;
        return returned;
    }

    /// <summary>
    /// Spends one charge at <paramref name="now"/>. Returns <see langword="false"/> when no charge is available.
    /// A spend from <see cref="MaxCharges"/> charges sets <see cref="RechargeLength"/>, <see cref="Rate"/> and
    /// the start of the recharge. A spend from fewer charges keeps the recharge in progress.
    /// </summary>
    /// <param name="now">Real millisecond of the spend.</param>
    /// <param name="rechargeLength">Cooldown milliseconds of one full recharge.</param>
    /// <param name="rate">Recovery rate from <paramref name="now"/>.</param>
    /// <param name="hold">Pauses progress until <see cref="Release"/>.</param>
    public bool Spend(int now, double rechargeLength, double rate, bool hold = false)
    {
        Advance(now);
        if (Charges <= 0)
        {
            return false;
        }

        var wasFull = Charges == MaxCharges;
        Charges--;
        if (wasFull)
        {
            RechargeLength = rechargeLength;
            ProgressLeft = rechargeLength;
            Rate = rate;
            OverallStart = now;
            ChargeStart = now;
            Held = hold;
        }

        return true;
    }

    /// <summary>
    /// Ends a hold and starts the recharge at <paramref name="now"/>. Changes nothing when the state is not
    /// <see cref="Held"/>.
    /// </summary>
    public void Release(int now, double rechargeLength, double rate)
    {
        if (!Held)
        {
            return;
        }

        Held = false;
        RechargeLength = rechargeLength;
        ProgressLeft = rechargeLength;
        Rate = rate;
        Timestamp = now;
        OverallStart = now;
        ChargeStart = now;
    }

    /// <summary>
    /// Takes <paramref name="amount"/> cooldown milliseconds off the recharge at <paramref name="now"/>,
    /// carrying the remainder into the next charge. Returns the cooldown milliseconds applied. A reduction past
    /// the last charge on cooldown is not applied.
    /// </summary>
    public double Reduce(int now, double amount)
    {
        Advance(now);
        if (amount <= 0 || !Recharging || Held)
        {
            return 0;
        }

        var left = amount;
        var applied = 0.0;
        while (Recharging && left > 0)
        {
            if (left < ProgressLeft)
            {
                ProgressLeft -= left;
                applied += left;
                left = 0;
            }
            else
            {
                applied += ProgressLeft;
                left -= ProgressLeft;
                Charges++;
                ChargeStart = now;
                ProgressLeft = Recharging ? RechargeLength : 0;
            }
        }

        return applied;
    }

    /// <summary>
    /// Adds a charge at <paramref name="now"/> and keeps the recharge in progress. Returns
    /// <see langword="false"/> when no charge is recharging.
    /// </summary>
    public bool GrantCharge(int now)
    {
        Advance(now);
        if (!Recharging)
        {
            return false;
        }

        Charges++;
        if (!Recharging)
        {
            ProgressLeft = 0;
            Held = false;
        }

        return true;
    }

    /// <summary>
    /// Returns the charge recharging at <paramref name="now"/> and starts a full recharge of the next charge.
    /// Returns <see langword="false"/> when no charge is recharging.
    /// </summary>
    public bool CompleteCharge(int now)
    {
        Advance(now);
        if (!Recharging)
        {
            return false;
        }

        Charges++;
        Held = false;
        ChargeStart = now;
        ProgressLeft = Recharging ? RechargeLength : 0;
        return true;
    }

    /// <summary>Returns every charge at <paramref name="now"/> and clears the progress and the hold.</summary>
    public void ResetAll(int now)
    {
        Charges = MaxCharges;
        ProgressLeft = 0;
        Held = false;
        Timestamp = Math.Max(Timestamp, now);
    }
}
