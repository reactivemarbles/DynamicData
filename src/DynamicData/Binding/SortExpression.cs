// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Binding;

/// <summary>
/// A value expression with sort direction.
/// </summary>
/// <typeparam name="T">The type of the item.</typeparam>
/// <remarks>
/// Initializes a new instance of the <see cref="SortExpression{T}"/> class.
/// </remarks>
/// <param name="expression">The expression.</param>
/// <param name="direction">The direction.</param>
public class SortExpression<T>(Func<T, IComparable> expression, SortDirection direction = SortDirection.Ascending)
{
    /// <summary>
    /// Gets the direction.
    /// </summary>
    public SortDirection Direction { get; } = direction;

    /// <summary>
    /// Gets the expression.
    /// </summary>
    public Func<T, IComparable> Expression { get; } = expression;
}
