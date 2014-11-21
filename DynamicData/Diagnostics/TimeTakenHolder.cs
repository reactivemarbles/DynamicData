using System;
using System.Collections.Generic;

namespace DynamicData.Diagnostics
{
    /// <summary>
    /// Decorates the original object with a timestamp
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public struct TimeTakenHolder<T> : IEquatable<TimeTakenHolder<T>>
    {
        private readonly T _value;
        private readonly TimeSpan _timeSpan;

        /// <summary>
        /// Gets the value.
        /// </summary>
        public T Value
        {
            get { return _value; }
        }

        /// <summary>
        /// Gets the time span.
        /// </summary>
        public TimeSpan TimeSpan
        {
            get { return _timeSpan; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TimeTakenHolder{T}"/> struct.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="timeSpan">The time span.</param>
        public TimeTakenHolder(T value, TimeSpan timeSpan)
            : this()
        {
            _value = value;
            _timeSpan = timeSpan;
        }

        #region Equality Members

        public bool Equals(TimeTakenHolder<T> other)
        {
            return EqualityComparer<T>.Default.Equals(_value, other._value) && _timeSpan.Equals(other._timeSpan);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is TimeTakenHolder<T> && Equals((TimeTakenHolder<T>) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (EqualityComparer<T>.Default.GetHashCode(_value)*397) ^ _timeSpan.GetHashCode();
            }
        }

        public static bool operator ==(TimeTakenHolder<T> left, TimeTakenHolder<T> right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(TimeTakenHolder<T> left, TimeTakenHolder<T> right)
        {
            return !left.Equals(right);
        }

        #endregion

        public override string ToString()
        {
            return string.Format("Value: {0}, {1}ms", _value, _timeSpan.TotalMilliseconds);
        }
    }
}