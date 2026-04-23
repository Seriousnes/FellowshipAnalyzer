# Plan: Modernize JSON Polymorphic Deserialization (.NET 10)

**TL;DR**: Replace `FSLJsonConverter<T>` (runtime assembly-scanning) with a Roslyn source generator that emits `[JsonDerivedType]` attributes on `partial class Event` at compile time, enabling .NET 10's native STJ polymorphism (which now handles out-of-order discriminators). Add a `JsonSerializerContext` for full AOT source generation. Delete both `FSLJsonConverter.cs` and `FSLEventDiscriminatorAttribute.cs` — all explicit discriminators already match the naming convention so the attribute is redundant.

---

## Phase 1 — Source Generator: `[JsonDerivedType]` emitter

**Step 1** — Add `JsonDerivedTypeGenerator.cs` to `FellowshipAnalyzer.Generators`
- `IIncrementalGenerator`, following the same pattern as `CombatLogParserGenerator.cs`
- **Trigger**: partial class in the compilation that has a `[JsonPolymorphic]` attribute
- **Collection**: find all non-abstract named types in the compilation that transitively inherit from the trigger class
- **Fabricated check**: walk the full base-type chain for `[FabricatedAttribute]` — Roslyn's `GetAttributes()` does NOT honor `Inherited = true`, so this must be done manually
- **Discriminator**: if class name ends in `"Event"` → `name[..^"Event".Length].ToLower()`; else skip
- **Output**: `Event.Derived.g.cs` — a `partial class Event` with one `[JsonDerivedType(typeof(X), "x")]` per qualifying derived type, sorted alphabetically for deterministic output

**Step 2** — Wire the generator to `FellowshipAnalyzer.Core.csproj`
- Add `<ProjectReference>` to `Generators.csproj` with `OutputItemType="Analyzer" ReferenceOutputAssembly="false"` (same pattern used for generators in other projects)

---

## Phase 2 — EventType property audit

**Step 3** — Audit usages of `Event.EventType` across all projects
- **Problem**: STJ's native polymorphism *consumes* the discriminator property and does NOT populate a matching regular property (`EventType`) — so after migration, `event.EventType` would always be `null`
- **If unused** → remove `EventType` and `[JsonPropertyName("type")]` from `Event.cs`
- **If used** → the generator should *also* emit per-concrete-type `EventType` overrides (e.g., `public override string EventType => "cast";`)

---

## Phase 3 — AOT `JsonSerializerContext`

**Step 4** — Create `FellowshipAnalyzerJsonContext.cs` in `FellowshipAnalyzer.Core/Serialization/`
- `[JsonSourceGenerationOptions(DefaultIgnoreCondition = WhenWritingNull, PropertyNamingPolicy = CamelCase, PropertyNameCaseInsensitive = true, UseStringEnumConverter = true)]`
- `[JsonSerializable(typeof(Event))]` — STJ's source generator follows `[JsonDerivedType]` attrs (emitted in Phase 1) to include all derived types transitively
- `internal partial class FellowshipAnalyzerJsonContext : JsonSerializerContext`

**Steps 5–7** — Update the three serialization setup sites to use `FellowshipAnalyzerJsonContext.Default.Options` instead of manually-built `JsonSerializerOptions`:
- `src/FellowshipAnalyzer.FellowshipLogs/Extensions/ServiceCollectionExtensions.cs` (server-side FellowshipLogs client)
- `src/FellowshipAnalyzer/FellowshipAnalyzer.Client/Program.cs`
- `tests/FellowshipAnalyzer.FellowshipLogs.Tests/PlayerEventLogParserTests.cs`

---

## Phase 4 — .NET 10 polymorphism options

**Step 8** — Update `[JsonPolymorphic]` on `Event.cs`:
- Add `UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization` skip unrecognized event types (same behavior as current `FSLJsonConverter`)

---

## Phase 5 — Cleanup

**Step 9** — Remove `[FSLEventDiscriminator(...)]` attribute usages from event files (11 occurrences)

**Step 10** — Delete `src/FellowshipAnalyzer.Core/Serialization/FSLJsonConverter.cs`

**Step 11** — Delete `src/FellowshipAnalyzer.Core/Events/FSLEventDiscriminatorAttribute.cs`

---

## Verification
1. `dotnet build` — confirms generator emits valid partial class, no type conflicts
2. Inspect generated `Event.Derived.g.cs` to verify expected types are listed
3. `dotnet test FellowshipAnalyzer.FellowshipLogs.Tests` — covers deserialization via `PlayerEventLogParserTests`
4. `dotnet test FellowshipAnalyzer.Heroes.Rime.Tests` — end-to-end analysis with deserialized events

---

## Relevant files
- `src/FellowshipAnalyzer.Core/Events/Event.cs` — base class; already `partial` and has `[JsonPolymorphic]`
- `src/FellowshipAnalyzer.Generators/CombatLogParserGenerator.cs` — reference for `IIncrementalGenerator` patterns
- `src/FellowshipAnalyzer.Generators/FellowshipAnalyzer.Generators.csproj` — `netstandard2.0`, `CodeAnalysis.CSharp 4.10.0`
- `src/FellowshipAnalyzer.Core/Serialization/FSLJsonConverter.cs` — DELETE
- `src/FellowshipAnalyzer.Core/Events/FSLEventDiscriminatorAttribute.cs` — DELETE