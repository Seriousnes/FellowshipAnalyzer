## Oblivion

Cast Oblivion while the tank holds under 40% of its maximum health in Stagger. Above that, cast Amend
Fate or Restore Continuity instead.

Each pull has `castWhileCleanseDue`: `never`, `sometimes` or `often`, for Oblivion cast with the tank
above 40% Stagger and a cleanse available. It says nothing about any other cast.

Patterns and their fixes:

- `Oblivion cast while a cleanse was due`: cleanse the tank with Amend Fate or Restore Continuity first,
  then return to Oblivion once the tank is under 40% Stagger.
