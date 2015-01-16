using System;

namespace DynamicData.Binding
{
    /// <summary>
    /// A value expression with sort direction
    /// </summary>
    /// <typeparam name="T"></typeparam>
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
}