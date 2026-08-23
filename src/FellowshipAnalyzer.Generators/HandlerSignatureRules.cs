using System.Collections.Immutable;

using Microsoft.CodeAnalysis;

namespace FellowshipAnalyzer.Generators;

internal enum HandlerReturnKind
{
    Void,
    Task,
    ValueTask,
    Unsupported,
}

/// <summary>
/// Single source of truth for "is this <c>[On&lt;TEvent&gt;]</c> handler parameter compatible
/// with the attribute's <c>TEvent</c>?". Consumed by both <see cref="ModuleGenerator"/> and
/// <c>OnHandlerSignatureAnalyzer</c> (source-linked) so the two cannot drift.
/// </summary>
internal static class HandlerSignatureRules
{
    private const string OneOfTypeName = "OneOf";
    private const string OneOfNamespace = "OneOf";
    private const string TasksNamespace = "System.Threading.Tasks";

    public static HandlerReturnKind ClassifyReturn(IMethodSymbol method)
    {
        if (method.ReturnsVoid) return HandlerReturnKind.Void;
        if (method.ReturnType is not INamedTypeSymbol returnType) return HandlerReturnKind.Unsupported;
        if (returnType.ContainingNamespace?.ToDisplayString() != TasksNamespace) return HandlerReturnKind.Unsupported;
        if (returnType.Arity > 1) return HandlerReturnKind.Unsupported;

        return returnType.Name switch
        {
            "Task" => HandlerReturnKind.Task,
            "ValueTask" => HandlerReturnKind.ValueTask,
            _ => HandlerReturnKind.Unsupported,
        };
    }

    /// <summary>
    /// Returns true when a value of <paramref name="eventType"/> is directly assignable to
    /// a parameter declared as <paramref name="param"/>: identity, base class, or interface.
    /// </summary>
    public static bool IsCompatibleParam(ITypeSymbol param, INamedTypeSymbol eventType)
    {
        if (SymbolEqualityComparer.Default.Equals(param, eventType)) return true;
        for (var b = eventType.BaseType; b is not null; b = b.BaseType)
            if (SymbolEqualityComparer.Default.Equals(param, b)) return true;
        foreach (var i in eventType.AllInterfaces)
            if (SymbolEqualityComparer.Default.Equals(param, i)) return true;
        return false;
    }

    /// <summary>
    /// Returns true when <paramref name="param"/> is a closed-generic <c>OneOf&lt;T0..Tn&gt;</c>
    /// from the <c>OneOf</c> namespace, with at least one type argument.
    /// </summary>
    public static bool IsOneOfParam(ITypeSymbol param, out INamedTypeSymbol oneOf)
    {
        if (param is INamedTypeSymbol named
            && named.IsGenericType
            && named.Name == OneOfTypeName
            && named.ContainingNamespace?.Name == OneOfNamespace
            && named.TypeArguments.Length > 0)
        {
            oneOf = named;
            return true;
        }
        oneOf = null!;
        return false;
    }

    /// <summary>
    /// Resolves which slot of a <c>OneOf&lt;T0..Tn&gt;</c> parameter should receive a value of
    /// <paramref name="eventType"/>. Selection: exact-match wins; otherwise the unique most-derived
    /// slot whose type is compatible (per <see cref="IsCompatibleParam"/>). Returns false on no
    /// match or unresolvable ambiguity.
    /// </summary>
    public static bool TryResolveOneOfSlot(
        INamedTypeSymbol oneOfParam,
        INamedTypeSymbol eventType,
        out int slotIndex,
        out string? ambiguityReason)
    {
        slotIndex = -1;
        ambiguityReason = null;

        var slots = oneOfParam.TypeArguments;

        var exactMatches = ImmutableArray.CreateBuilder<int>();
        for (var i = 0; i < slots.Length; i++)
        {
            if (slots[i] is INamedTypeSymbol named &&
                SymbolEqualityComparer.Default.Equals(named, eventType))
            {
                exactMatches.Add(i);
            }
        }

        if (exactMatches.Count == 1)
        {
            slotIndex = exactMatches[0];
            return true;
        }
        if (exactMatches.Count > 1)
        {
            ambiguityReason = "OneOf has duplicate slots for " + eventType.Name;
            return false;
        }

        var compatible = ImmutableArray.CreateBuilder<(int Index, INamedTypeSymbol Type)>();
        for (var i = 0; i < slots.Length; i++)
        {
            if (slots[i] is not INamedTypeSymbol named) continue;
            if (IsCompatibleParam(named, eventType))
                compatible.Add((i, named));
        }

        if (compatible.Count == 0)
        {
            ambiguityReason = "no OneOf slot is assignable from " + eventType.Name;
            return false;
        }

        if (compatible.Count == 1)
        {
            slotIndex = compatible[0].Index;
            return true;
        }

        var mostDerived = -1;
        for (var i = 0; i < compatible.Count; i++)
        {
            var dominatedByOther = false;
            for (var j = 0; j < compatible.Count; j++)
            {
                if (i == j) continue;
                if (IsAssignableTo(compatible[j].Type, compatible[i].Type))
                {
                    dominatedByOther = true;
                    break;
                }
            }
            if (!dominatedByOther)
            {
                if (mostDerived != -1)
                {
                    ambiguityReason = "OneOf slots "
                        + compatible[mostDerived].Type.Name + " and " + compatible[i].Type.Name
                        + " are both compatible with " + eventType.Name
                        + " and neither dominates the other";
                    return false;
                }
                mostDerived = i;
            }
        }

        if (mostDerived == -1)
        {
            ambiguityReason = "no unique most-derived OneOf slot for " + eventType.Name;
            return false;
        }

        slotIndex = compatible[mostDerived].Index;
        return true;
    }

    private static bool IsAssignableTo(INamedTypeSymbol from, INamedTypeSymbol to) =>
        IsCompatibleParam(to, from);
}
