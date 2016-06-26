using System;
using System.Collections.Generic;

namespace DynamicData.Kernel
{
    /// <summary>
    /// An error container used to report errors from within dynamic data operators
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    public sealed class Error<TObject, TKey> : IKeyValue<TObject, TKey>, IEquatable<Error<TObject, TKey>>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="T:System.Object"/> class.
        /// </summary>
        public Error(Exception exception, TObject value, TKey key)
        {
            Exception = exception;
            Value = value;
            Key = key;
        }

        /// <summary>
        /// Gets the key.
        /// </summary>
        public TKey Key { get; }

        /// <summary>
        /// Gets the object.
        /// </summary>
        public TObject Value { get; }

        /// <summary>
        /// The exception.
        /// </summary>
        public Exception Exception { get; }

        #region Equality members

        /// <summary>
        /// Implements the operator ==.
        /// </summary>
        /// <param name="left">The left.</param>
        /// <param name="right">The right.</param>
        /// <returns>
        /// The result of the operator.
        /// </returns>
        public static bool operator ==(Error<TObject, TKey> left, Error<TObject, TKey> right)
        {
            return Equals(left, right);
        }

        /// <summary>
        /// Implements the operator !=.
        /// </summary>
        /// <param name="left">The left.</param>
        /// <param name="right">The right.</param>
        /// <returns>
        /// The result of the operator.
        /// </returns>
        public static bool operator !=(Error<TObject, TKey> left, Error<TObject, TKey> right)
        {
            return !Equals(left, right);
        }

        /// <summary>
        /// Equalses the specified other.
        /// </summary>
        /// <param name="other">The other.</param>
        /// <returns></returns>
        public bool Equals(Error<TObject, TKey> other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return EqualityComparer<TKey>.Default.Equals(Key, other.Key) && EqualityComparer<TObject>.Default.Equals(Value, other.Value) && Equals(Exception, other.Exception);
        }

        /// <summary>
        /// Indicates whether this instance and a specified object are equal.
        /// </summary>
        /// <returns>
        /// true if <paramref name="obj"/> and this instance are the same type and represent the same value; otherwise, false.
        /// </returns>
        /// <param name="obj">Another object to compare to. </param>
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj is Error<TObject, TKey> && Equals((Error<TObject, TKey>)obj);
        }

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        /// <returns>
        /// A 32-bit signed integer that is the hash code for this instance.
        /// </returns>
        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = EqualityComparer<TKey>.Default.GetHashCode(Key);
                hashCode = (hashCode * 397) ^ EqualityComparer<TObject>.Default.GetHashCode(Value);
                hashCode = (hashCode * 397) ^ (Exception != null ? Exception.GetHashCode() : 0);
                return hashCode;
            }
        }

        #endregion

        /// <summary>
        /// Returns the fully qualified type name of this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="T:System.String"/> containing a fully qualified type name.
        /// </returns>
        public override string ToString()
        {
            return $"Key: {Key}, Value: {Value}, Exception: {Exception}";
        }
    }
}
