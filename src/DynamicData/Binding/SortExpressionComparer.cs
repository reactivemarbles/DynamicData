using System;
using System.Collections.Generic;

namespace DynamicData.Binding
{
    /// <summary>
    /// Generic sort expression to help create inline sorting for the .Sort(IComparer comparer) operator
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class SortExpressionComparer<T> : List<SortExpression<T>>, IComparer<T>
    {
        /// <summary>
        /// Compares x and y
        /// </summary>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        /// <returns></returns>
        public int Compare(T x, T y)
        {
            foreach (var item in this)
            {
                if (x == null && y == null) continue;
                if (x == null) return -1;
                if (y == null) return 1;

                var xValue = item.Expression(x);
                var yValue = item.Expression(y);

                if (xValue == null && yValue == null) continue;
                if (xValue == null) return -1;
                if (yValue == null) return 1;

                int result = xValue.CompareTo(yValue);
                if (result == 0) continue;

                return (item.Direction == SortDirection.Ascending) ? result : -result;
            }
            return 0;
        }

        /// <summary>
        /// Create an ascending sort expression
        /// </summary>
        /// <param name="expression">The expression.</param>
        /// <returns></returns>
        public static SortExpressionComparer<T> Ascending(Func<T, IComparable> expression)
        {
            return new SortExpressionComparer<T> { new SortExpression<T>(expression) };
        }

        /// <summary>
        /// Create an descending sort expression.
        /// </summary>
        /// <param name="expression">The expression.</param>
        /// <returns></returns>
        public static SortExpressionComparer<T> Descending(Func<T, IComparable> expression)
        {
            return new SortExpressionComparer<T> { new SortExpression<T>(expression, SortDirection.Descending) };
        }

        /// <summary>
        /// Adds an additional ascending sort expression
        /// </summary>
        /// <param name="expression">The expression.</param>
        /// <returns></returns>
        public SortExpressionComparer<T> ThenByAscending(Func<T, IComparable> expression)
        {
            Add(new SortExpression<T>(expression));
            return this;
        }

        /// <summary>
        ///  Adds an additional desccending sort expression
        /// </summary>
        /// <param name="expression">The expression.</param>
        /// <returns></returns>
        public SortExpressionComparer<T> ThenByDescending(Func<T, IComparable> expression)
        {
            Add(new SortExpression<T>(expression, SortDirection.Descending));
            return this;
        }
    }
}
