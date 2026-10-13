## Amend Fate and Restore Continuity

Cast Amend Fate or Restore Continuity when the target holds more than the Stagger removed. Always cast one
of them when the tank passes 40% of its maximum health in Stagger. Below that, cast Oblivion instead. With
Echoes of Divinity, spend Uchronia on Restore Continuity to apply it to the tank, and let it run its full
duration before the next cleanse.

Each pull has one entry in `casts` per cleanse:

- `spell` and `free`: Uchronia or Epoch Break for a free cast.
- `tankUnderCleanseLine`: the tank held under 40% Stagger.
- `targetHeldLessThanOneCast`: the target held less Stagger than one cast removes.
- `healedLessThanOblivion`: the cast healed less than one Oblivion.
- With Echoes of Divinity only: `appliedEchoes`, the cast applied it to the tank, and
  `refreshedRunningEchoes`, it refreshed one still running.

A cleanse with the tank under 40% is right when the target held a full cast of Stagger and the cast healed
more than an Oblivion.

A low-Stagger cleanse is one cast with the tank under 40% Stagger.

Patterns and their fixes:

- `low-Stagger cleanses on a target holding less than a full cast`: cast Oblivion until the tank nears 40%, then cleanse a full cast's worth.
- `low-Stagger cleanses that healed less than an Oblivion`: cast Oblivion instead.
- `refreshed running Echoes of Divinity`: let it run its full duration before the next cleanse.
- `Uchronia spent on Amend Fate`, with Echoes of Divinity taken: spend Uchronia on Restore Continuity.
