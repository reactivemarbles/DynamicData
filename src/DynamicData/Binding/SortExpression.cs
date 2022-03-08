// Copyright (c) 2011-2020 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;

namespace DynamicData.Binding;

/// <summary>
/// A value expression with sort direction.
/// </summary>
/// <typeparam name="T">The type of the item.</typeparam>
public class SortExpression<T>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SortExpression{T}"/> class.
    /// </summary>
    /// <param name="expression">The expression.</param>
    /// <param name="direction">The direction.</param>
    public SortExpression(Func<T, IComparable> expression, SortDirection direction = SortDirection.Ascending)
    {
        Expression = expression;
        Direction = direction;
    }

    /// <summary>
    /// Gets the direction.
    /// </summary>
    public SortDirection Direction { get; }

    /// <summary>
    /// Gets the expression.
    /// </summary>
    public Func<T, IComparable> Expression { get; }
}
