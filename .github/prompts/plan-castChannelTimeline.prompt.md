## Plan: Channel Spell Timeline

Fix how channelled spells are handled in FellowshipAnalyzer by preserving WoWA-style cast/channel relationships for later analyzers while rendering only the correct channel timeline shape: real non-channel casts show cast and GCD bars, while channels show one icon at `beginchannel`, a GCD starting immediately at `beginchannel`, and a channel bar ending at `endchannel`. The recommended approach is a normalizer-level link contract plus a small timeline update that consumes those links. Chronoshift (spell ID 1558) is the concrete known case that exposed the gap and will serve as the primary validation example.

**Steps**
1. Update `CastLinkNormalizer` to recognize a `CastEvent` followed by a matching `BeginChannelEvent` for the same spell/source and then linked `EndChannelEvent`, treating the three events as one cast-to-channel sequence rather than two separate timeline actions. There may be other events in between, but the key is that the `cast` and `beginchannel` appear very close together in time and share the same spell GUID, while the `endchannel` matches the same pending channel sequence. 
2. Define the cast/channel relationship contract to match WoWA semantics for downstream analysis:
   `BeginCastEvent.Channel -> BeginChannelEvent`
   `CastEvent.Channel -> EndChannelEvent`
   `EndChannelEvent.BeginChannel -> BeginChannelEvent`
   This gives listeners a path from cast to endchannel and from endchannel back to beginchannel.
3. Review the FellowshipAnalyzer event model and align it to that contract. If `CastEvent.Channel` currently points to the wrong type, the plan should include correcting the property type rather than overloading it with a different meaning.
4. Populate those links during normalization: when a `CastEvent` is followed by a `BeginChannelEvent` from the same source with the same spell GUID within a short time window, treat them as one cast-to-channel sequence. The `endchannel` is matched to the same pending channel sequence. The time window should be generous enough to tolerate log jitter but tight enough to avoid false matches with unrelated spells.
5. Preserve the existing `BeginCastEvent -> CastEvent` completion link so regular casts still work exactly as they do today, and add `BeginCastEvent.Channel` only when the cast transitions into a channel.
6. Update `CastBar.razor` to render two distinct categories only:
   A) real, non-faked casts with no channel relationship render as cast items with cast-time and GCD bars
   B) channel sequences render only as channel items with icon and GCD at `beginchannel` and channel width extending to `endchannel`
7. Explicitly exclude cast items that are associated with channels in any way from the cast timeline row, so Chronoshift does not produce a second main-row or lower/off-GCD icon.
8. Keep channel GCD/cooldown semantics unchanged: they begin immediately at `beginchannel`, regardless of when the channel ends, and shortened channels are not treated as cancelled casts.
9. Verify how channel width is sourced. If `EndChannelEvent.Start` and `Duration` are not reliably populated in Fellowship logs, add a minimal fallback in the timeline using `BeginChannelEvent.Timestamp` and `EndChannelEvent.Timestamp - BeginChannelEvent.Timestamp`.
10. Validate using the local Chronoshift fixture as a concrete reference: `cast(fake+activation)` at `20766424`, `begincast` at `20766424`, `cast` at `20767260`, `beginchannel` at `20767260`, `endchannel` at `20768760`, fight start `20746282`. The fix must generalise to any channelled spell, not just Chronoshift.

**Relevant files**
- `g:/source/FellowshipAnalyzer/src/FellowshipAnalyzer.Core/Analysis/Normalizers/CastLinkNormalizer.cs` — add and populate the cast-to-channel relationship contract.
- `g:/source/FellowshipAnalyzer/src/FellowshipAnalyzer.Components/Timeline/CastBar.razor` — consume those links so only non-channel casts render as cast items and channels render as channel items.
- `g:/source/FellowshipAnalyzer/src/FellowshipAnalyzer.Core/Events/CastEvent.cs` — verify or adjust the `Channel` property so it matches the desired cast-to-endchannel contract.
- `g:/source/FellowshipAnalyzer/src/FellowshipAnalyzer.Core/Events/BeginCastEvent.cs` — use the `Channel` property for begincast-to-beginchannel linkage when applicable.
- `g:/source/FellowshipAnalyzer/src/FellowshipAnalyzer.Core/Events/EndChannelEvent.cs` — retain the reverse `BeginChannel` link and confirm duration data shape.
- `g:/source/FellowshipAnalyzer/src/FellowshipAnalyzer.Core/Analysis/Modules/GlobalCooldown.cs` — verify channel GCD ownership remains on `BeginChannelEvent` and is not accidentally shifted back to the cast item.
- `g:/source/FellowshipAnalyzer/src/FellowshipAnalyzer.FellowshipLogs/raw-report.json` — use the Chronoshift sequence above as the local verification fixture.

**Verification**
1. Build the touched project: `dotnet build g:/source/FellowshipAnalyzer/src/FellowshipAnalyzer.Components/FellowshipAnalyzer.Components.csproj`.
2. Inspect the Chronoshift timeline (as the concrete test case) and confirm there is exactly one icon on the main cast row.
3. Confirm that icon is positioned at `beginchannel`, not at `cast` or `endchannel`.
4. Confirm the duplicate lower/off-GCD icon is gone because the cast item is suppressed by channel linkage.
5. Confirm the GCD still starts immediately at `beginchannel` and remains visible.
6. Confirm the channel bar ends at `endchannel` and shortened channels remain shorter channels rather than cancelled casts.
7. Confirm downstream event consumers can navigate cast -> endchannel -> beginchannel via the established links.

**Decisions**
- Included scope: cast-to-channel link modeling and timeline rendering for all channelled spells in FellowshipAnalyzer. Chronoshift is the known triggering case but the fix must be general.
- Excluded scope: broader analyzer redesign beyond the cast/channel relationship contract needed for timeline and later listeners.
- Decided behavior: `beginchannel` is the timeline anchor for channels when both `cast` and `beginchannel` exist.
- Decided behavior: channel spells are not rendered as standalone casts in the timeline when a channel relationship exists.
- Decided behavior: early channel end is not a cancelled cast; it is a successful channel with shorter duration.

**Further Considerations**
1. If the current C# `CastEvent.Channel` type differs from the desired WoWA contract, align the event model first so later modules do not inherit an ambiguous API.
2. If any existing analyzer currently assumes channel-linked casts still appear as normal cast items, note that as a behavior change and verify those consumers explicitly during implementation.
