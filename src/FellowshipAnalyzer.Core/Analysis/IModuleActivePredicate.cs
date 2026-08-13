namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Compile-time predicate consulted before a module is constructed. When
/// <see cref="IsActive"/> returns <c>false</c> the module is not instantiated, its
/// constructor never runs, and its <c>[On&lt;T&gt;]</c> handlers never subscribe.
/// </summary>
/// <remarks>
/// <para>
/// Implement on a small sealed type and pass it as the type argument to
/// <see cref="ActiveWhenAttribute{TPredicate}"/>. The interface is static-abstract so the
/// generator can dispatch through a switch on module type without runtime reflection — the
/// emitted call site looks like <c>MyPredicate.IsActive(parseContext)</c>.
/// </para>
/// <para>
/// Use this for static-context activation only (hero, dungeon encounter id, player id, master
/// data presence). For dynamic activation that depends on events fired during dispatch
/// (combatant gear loaded by the Combatants module, talents observed from a buff, etc.),
/// keep using the mutable <see cref="Module.Active"/> flag — that check survives because
/// the <see cref="EventEmitter"/> reads it per event.
/// </para>
/// </remarks>
public interface IModuleActivePredicate
{
    /// <summary>Determines, from static parse context alone, whether the module this predicate guards should be constructed.</summary>
    /// <param name="context">The parse-time context available before any modules are constructed.</param>
    /// <returns><c>true</c> if the module should be instantiated and subscribed; otherwise <c>false</c>.</returns>
    static abstract bool IsActive(ParseContext context);
}
