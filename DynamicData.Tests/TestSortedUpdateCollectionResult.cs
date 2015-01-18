using System;
using System.Collections.Generic;

namespace DynamicData.Tests
{
    public enum SortDirection
    {
        Ascending,
        Descending
    }

    public class SortExpression<T>
    {
        private readonly SortDirection _direction;
        private readonly Func<T, IComparable> _expression;

        public SortExpression(Func<T, IComparable> expression, SortDirection direction = SortDirection.Ascending)
        {
            _expression = expression;
            _direction = direction;
        }

        public SortDirection Direction
        {
            get { return _direction; }
        }

        public Func<T, IComparable> Expression
        {
            get { return _expression; }
        }
    }

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
    }
}