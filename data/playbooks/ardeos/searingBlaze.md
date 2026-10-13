## Searing Blaze

Searing Blaze never drops off a boss, and goes on every enemy in a pack.

Each pull has `applied`, true when Searing Blaze landed at least once. A boss has `uptime`, `full`,
`partial` or `low`, and `droppedOff`, true when Searing Blaze fell off before a reapplication. A pack has
`spread`, `full`, `partial` or `low` across its enemies.

Patterns and their fixes:

- `uptime short of full`: keep Searing Blaze on the boss the whole fight. When `dropped off` also showed,
  reapply Searing Blaze as soon as it drops. Uptime and drop-off on the boss are one insight.
- `spread short of full`: apply Searing Blaze to every enemy in the pack.
