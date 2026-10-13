## Wildfire Combo

Apply as many DoTs as possible before Wildfire, ideally with 2 Engulfing Flames instances. Once Wildfire is
cast, apply no more DoTs and cast Detonate as many times as possible before the buff ends.

Each pull has one entry in `windows` per Wildfire cast:

- `dotsMissing` names the DoTs absent from the target at the cast.
- `enoughDots` is true when enough DoTs were ticking for a complete setup; `twoEngulfingFlames` when the
  target carried 2 Engulfing Flames instances.
- `followUpComplete` is true when enough Detonate casts landed inside the buff.
- `dotCastAfter` is true when a DoT was cast just after Wildfire.
- `castsBefore` and `castsAfter` are the fire casts just before and just after Wildfire, in order.

Patterns and their fixes:

- `<DoT> missing` or `short of enough DoTs`: apply the missing DoT before Wildfire.
- `short of both Engulfing Flames instances`: cast both Engulfing Flames charges before Wildfire.
- `incomplete follow-up` or `DoT casts after Wildfire`: stop applying DoTs once Wildfire is cast and cast
  Detonate until the buff ends.
