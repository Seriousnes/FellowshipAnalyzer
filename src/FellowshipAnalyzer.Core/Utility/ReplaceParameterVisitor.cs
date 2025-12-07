using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;

namespace FellowshipAnalyzer.Core.Utility;

internal sealed class ReplaceParameterVisitor(ParameterExpression oldParameter, ParameterExpression newParameter) : ExpressionVisitor
{
    [return: NotNullIfNotNull(nameof(node))]
    protected override Expression? VisitParameter(ParameterExpression node)
    {
        if (ReferenceEquals(node, oldParameter))
        {
            return newParameter;
        }

        return base.VisitParameter(node);
    }
}
