using System.Collections.Generic;
using DynamicData.Kernel;

namespace DynamicData.Internal
{
    /// <summary>
    ///     Staging object for ManyTransform.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal struct TransformedItem<T>
    {
        private readonly T _current;
        private readonly Optional<T> _previous;
        private readonly ChangeReason _reason;

        public TransformedItem(ChangeReason reason, T current)
            : this(reason, current, Optional.None<T>())
        {
        }

        public TransformedItem(ChangeReason reason, T current, Optional<T> previous)
        {
            _reason = reason;
            _current = current;
            _previous = previous;
        }

        public ChangeReason Reason { get { return _reason; } }

        public T Current { get { return _current; } }

        public Optional<T> Previous { get { return _previous; } }

        #region Equality

        public bool Equals(TransformedItem<T> other)
        {
            return _reason == other._reason && EqualityComparer<T>.Default.Equals(_current, other._current) &&
                   _previous.Equals(other._previous);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is TransformedItem<T> && Equals((TransformedItem<T>)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (int)_reason;
                hashCode = (hashCode * 397) ^ EqualityComparer<T>.Default.GetHashCode(_current);
                hashCode = (hashCode * 397) ^ _previous.GetHashCode();
                return hashCode;
            }
        }

        #endregion

        public override string ToString()
        {
            return string.Format("Reason: {0}, Current: {1}, Previous: {2}", Reason, Current, Previous);
        }
    }
}
