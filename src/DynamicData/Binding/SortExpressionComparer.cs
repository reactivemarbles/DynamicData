// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Binding;

/// <summary>
/// Generic sort expression to help create inline sorting for the .Sort(IComparer comparer) operator.
/// </summary>
/// <typeparam name="T">The item to sort against.</typeparam>
public class SortExpressionComparer<T> : List<SortExpression<T>>, IComparer<T>
{
    /// <summary>
    /// Create an ascending sort expression.
    /// </summary>
    /// <param name="expression">The expression.</param>
    /// <returns>A comparer in ascending order.</returns>
    public static SortExpressionComparer<T> Ascending(Func<T, IComparable> expression) => new() { new(expression) };

    /// <summary>
    /// Create an descending sort expression.
    /// </summary>
    /// <param name="expression">The expression.</param>
    /// <returns>A comparer in descending order.</returns>
    public static SortExpressionComparer<T> Descending(Func<T, IComparable> expression) => new() { new(expression, SortDirection.Descending) };

    /// <inheritdoc/>
    public int Compare(T? x, T? y)
    {
        foreach (var item in this)
        {
            if (x is null && y is null)
            {
                continue;
            }

            if (x is null)
            {
                return -1;
            }

            if (y is null)
            {
                return 1;
            }

            var xValue = item.Expression(x);
            var yValue = item.Expression(y);

            if (xValue is null && yValue is null)
            {
                continue;
            }

            if (xValue is null)
            {
                return -1;
            }

            if (yValue is null)
            {
                return 1;
            }

            var result = xValue.CompareTo(yValue);
            if (result == 0)
            {
                continue;
            }

            return (item.Direction == SortDirection.Ascending) ? result : -result;
        }

        return 0;
    }

    /// <summary>
    /// Adds an additional ascending sort expression.
    /// </summary>
    /// <param name="expression">The expression.</param>
    /// <returns>A comparer in ascending order first taking into account the comparer passed in.</returns>
    public SortExpressionComparer<T> ThenByAscending(Func<T, IComparable> expression)
    {
        Add(new SortExpression<T>(expression));
        return this;
    }

    /// <summary>
    ///  Adds an additional descending sort expression.
    /// </summary>
    /// <param name="expression">The expression.</param>
    /// <returns>A comparer in descending order first taking into account the comparer passed in.</returns>
    public SortExpressionComparer<T> ThenByDescending(Func<T, IComparable> expression)
    {
        Add(new SortExpression<T>(expression, SortDirection.Descending));
        return this;
    }
}
