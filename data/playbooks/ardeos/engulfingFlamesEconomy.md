## Engulfing Flames Economy

Pool Engulfing Flames charges to have 2 available for a Wildfire window, and avoid sitting at maximum
charges for long stretches.

Each pull has `wastedCharges`, true when a recharge passed with Engulfing Flames at maximum charges, and
one entry in `setups` per Wildfire cast:

- `setup` is `full` with both charges cast before Wildfire, `partial` with one, `low` with none.
- `fullChargesWhenWildfireReady` is true when Engulfing Flames held maximum charges as Wildfire came off
  cooldown.

Patterns and their fixes:

- `wasted charges`: spend one charge whenever Engulfing Flames is full, then save the next two for the
  setup before Wildfire.
- `setup short of both charges`: hold both charges and cast both right before Wildfire.
