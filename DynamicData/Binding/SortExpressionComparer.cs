using System;
using System.Collections.Generic;

namespace DynamicData.Binding
{
    /// <summary>
    /// Class for constructing sort expressions 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class SortExpressionComparer<T> : List<SortExpression<T>>, IComparer<T>
    {

        public int Compare(T x, T y)
        {
            foreach (var item in this)
            {
                var yValue = item.Expression(y);

                int result = item.Expression(x).CompareTo(yValue);
                if (result == 0)
                {
                    continue;
                }

                return (item.Direction == SortDirection.Ascending) ? result : -result;
            }
            return 0;
        }

        public static SortExpressionComparer<T> Ascending(Func<T, IComparable> expression)
        {
            return new SortExpressionComparer<T> { new SortExpression<T>(expression) };
        }

        public static SortExpressionComparer<T> Descending(Func<T, IComparable> expression)
        {
            return new SortExpressionComparer<T> { new SortExpression<T>(expression, SortDirection.Descending) };
        }

        public SortExpressionComparer<T> ThenByAscending(Func<T, IComparable> expression)
        {
            Add(new SortExpression<T>(expression));
            return this;
        }

        public SortExpressionComparer<T> ThenByDescending(Func<T, IComparable> expression)
        {
            Add(new SortExpression<T>(expression, SortDirection.Descending));
            return this;
        }
    }
}