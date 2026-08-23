namespace FellowshipAnalyzer.Core.Analysis;

/// <summary>
/// Declares a dependency on a sibling module. Applied to a module, analyzer, normalizer, or
/// resource-tracker class, <c>[Dependency&lt;TDependency&gt;]</c> makes the source generator emit a
/// primary-constructor parameter <c>Lazy&lt;TDependency&gt;</c> and a cached read-only accessor
/// property named after <typeparamref name="TDependency"/> - e.g. <c>[Dependency&lt;Haste&gt;]</c> yields
/// <c>private Haste Haste =&gt; …;</c>. The dependency resolves per analysis run through the parser's
/// module cache, exactly as a hand-written <c>Lazy&lt;TDependency&gt;</c> constructor parameter would.
/// <para>
/// Use this in place of a hand-written constructor when the class depends only on sibling modules.
/// A class that also needs outer-DI services (for example <c>ILogger</c>) keeps a hand-written
/// constructor instead; declaring both forms on one class reports FA0018 and the attribute is
/// ignored in favour of the constructor.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class DependencyAttribute<TDependency> : Attribute
    where TDependency : Module
{
}
