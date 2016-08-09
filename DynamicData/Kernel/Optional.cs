using System;
using System.Collections.Generic;

namespace DynamicData.Kernel
{
    /// <summary>
    /// Optional factory class
    /// </summary>
    public static class Optional
    {
        /// <summary>
        ///Wraps the specified value in an Optional container
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value">The value.</param>
        /// <returns></returns>
        public static Optional<T> Some<T>(T value)
        {
            return new Optional<T>(value);
        }

        /// <summary>
        /// Returns an None optional value for the specified type.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static Optional<T> None<T>()
        {
            return Optional<T>.None;
        }
    }

    /// <summary>
    /// The equivalent of a nullable type which works on value and reference types
    /// </summary>
    /// <typeparam name="T">The underlying value type of the <see cref="T:System.Nullable`1"/> generic type.</typeparam><filterpriority>1</filterpriority>
    public struct Optional<T> : IEquatable<Optional<T>>
    {
        private readonly T _value;

        /// <summary>
        /// The default valueless optional
        /// </summary>
        public static readonly Optional<T> None = default(Optional<T>);

        /// <summary>
        /// Initializes a new instance of the <see cref="Optional{T}"/> struct.
        /// </summary>
        /// <param name="value">The value.</param>
        internal Optional(T value)
        {
            if (ReferenceEquals(value, null))
            {
                HasValue = false;
                _value = default(T);
            }
            else
            {
                _value = value;
                HasValue = true;
            }
        }

        /// <summary>
        /// Creates the specified value.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns></returns>
        public static Optional<T> Create(T value)
        {
            return new Optional<T>(value);
        }

        /// <summary>
        /// Gets a value indicating whether the current <see cref="T:System.Nullable`1"/> object has a value.
        /// </summary>
        /// 
        /// <returns>
        /// true if the current <see cref="T:System.Nullable`1"/> object has a value; false if the current <see cref="T:System.Nullable`1"/> object has no value.
        /// </returns>
        public bool HasValue { get; }

        /// <summary>
        /// Gets the value of the current <see cref="T:System.Nullable`1"/> value.
        /// </summary>
        /// 
        /// <returns>
        /// The value of the current <see cref="T:System.Nullable`1"/> object if the <see cref="P:System.Nullable`1.HasValue"/> property is true. An exception is thrown if the <see cref="P:System.Nullable`1.HasValue"/> property is false.
        /// </returns>
        /// <exception cref="T:System.InvalidOperationException">The <see cref="P:System.Nullable`1.HasValue"/> property is false.</exception>
        public T Value
        {
            get
            {
                if (!HasValue)
                {
                    throw new InvalidOperationException("Optional<T> has no value");
                }
                return _value;
            }
        }

        /// <summary>
        /// Implicit cast from the vale to the optional
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns></returns>
        public static implicit operator Optional<T>(T value)
        {
            return new Optional<T>(value);
        }

        /// <summary>
        /// Explicit cast from option to valiue
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns></returns>
        public static explicit operator T(Optional<T> value)
        {
            return value.Value;
        }

        #region Equality members

        /// <summary>
        /// Implements the operator ==.
        /// </summary>
        /// <param name="left">The left.</param>
        /// <param name="right">The right.</param>
        /// <returns>
        /// The result of the operator.
        /// </returns>
        public static bool operator ==(Optional<T> left, Optional<T> right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Implements the operator !=.
        /// </summary>
        /// <param name="left">The left.</param>
        /// <param name="right">The right.</param>
        /// <returns>
        /// The result of the operator.
        /// </returns>
        public static bool operator !=(Optional<T> left, Optional<T> right)
        {
            return !left.Equals(right);
        }

        /// <summary>
        /// Equalses the specified other.
        /// </summary>
        /// <param name="other">The other.</param>s
        /// <returns></returns>
        public bool Equals(Optional<T> other)
        {
            if (!HasValue) return !other.HasValue;
            if (!other.HasValue) return false;
            return HasValue.Equals(other.HasValue) && EqualityComparer<T>.Default.Equals(_value, other._value);
        }

        /// <summary>
        /// Determines whether the specified <see cref="System.Object" />, is equal to this instance.
        /// </summary>
        /// <param name="obj">The <see cref="System.Object" /> to compare with this instance.</param>
        /// <returns>
        ///   <c>true</c> if the specified <see cref="System.Object" /> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is Optional<T> && Equals((Optional<T>)obj);
        }

        /// <summary>
        /// Retrieves the hash code of the object returned by the <see cref="P:System.Nullable`1.Value"/> property.
        /// </summary>
        /// 
        /// <returns>
        /// The hash code of the object returned by the <see cref="P:System.Nullable`1.Value"/> property if the <see cref="P:System.Nullable`1.HasValue"/> property is true, or zero if the <see cref="P:System.Nullable`1.HasValue"/> property is false.
        /// </returns>
        public override int GetHashCode()
        {
            unchecked
            {
                return (HasValue.GetHashCode() * 397) ^ EqualityComparer<T>.Default.GetHashCode(_value);
            }
        }

        #endregion

        /// <summary>
        /// Returns a <see cref="System.String" /> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String" /> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            return !HasValue ? "<None>" : _value.ToString();
        }
    }
}
