using System;
using System.Collections.Generic;
#pragma warning disable 1591

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


        public static bool operator ==(Error<TObject, TKey> left, Error<TObject, TKey> right)
        {
            return Equals(left, right);
        }


        public static bool operator !=(Error<TObject, TKey> left, Error<TObject, TKey> right)
        {
            return !Equals(left, right);
        }

        /// <inheritdoc />
        public bool Equals(Error<TObject, TKey> other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return EqualityComparer<TKey>.Default.Equals(Key, other.Key) && EqualityComparer<TObject>.Default.Equals(Value, other.Value) && Equals(Exception, other.Exception);
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj is Error<TObject, TKey> && Equals((Error<TObject, TKey>)obj);
        }

        /// <inheritdoc />
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

        /// <inheritdoc />
        public override string ToString()
        {
            return $"Key: {Key}, Value: {Value}, Exception: {Exception}";
        }
    }
}
