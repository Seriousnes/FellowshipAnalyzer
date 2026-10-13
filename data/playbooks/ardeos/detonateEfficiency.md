## Detonate

Cast Detonate with as many DoTs active as possible, or cast it early to avoid capping Embers. With
Apocalyptic Surge, avoid letting its stacks expire unused.

Each pull has one entry in `casts` per Detonate cast:

- `dotStack` is `full`, `partial` or `low`: how many DoT instances each enemy carried.
- `dotsMissing` names the DoTs on no enemy at the cast.
- `free` is true when Apocalyptic Surge made the cast free.

With Apocalyptic Surge, `surgeStacksExpired` is true on a pull where a stack expired unused.

Patterns and their fixes:

- `short of a full stack`: apply the missing DoTs to the pack before casting Detonate.
- `<DoT> missing`: keep that DoT on the pack.
- `expired Apocalyptic Surge stacks`: spend the free Detonate before the stack expires.
