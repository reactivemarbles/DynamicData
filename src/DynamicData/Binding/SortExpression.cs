using System;

namespace DynamicData.Binding
{
    /// <summary>
    /// A value expression with sort direction
    /// </summary>
    /// <typeparam name="T"></typeparam>
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
        /// Gets or sets the direction.
        /// </summary>
        public SortDirection Direction { get; }

        /// <summary>
        /// Gets or sets the expression.
        /// </summary>
        public Func<T, IComparable> Expression { get; }
    }
}
