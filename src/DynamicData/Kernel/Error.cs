// Copyright (c) 2011-2020 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

#pragma warning disable 1591

namespace DynamicData.Kernel
{
    /// <summary>
    /// An error container used to report errors from within dynamic data operators.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    public sealed class Error<TObject, TKey> : IKeyValue<TObject, TKey>, IEquatable<Error<TObject, TKey>>
        where TKey : notnull
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Error{TObject, TKey}"/> class.
        /// </summary>
        /// <param name="exception">The exception that caused the error.</param>
        /// <param name="value">The value for the error.</param>
        /// <param name="key">The key for the error.</param>
        public Error(Exception? exception, TObject value, TKey key)
        {
            Exception = exception;
            Value = value;
            Key = key;
        }

        /// <summary>
        /// Gets the exception.
        /// </summary>
        public Exception? Exception { get; }

        /// <summary>
        /// Gets the key.
        /// </summary>
        public TKey Key { get; }

        /// <summary>
        /// Gets the object.
        /// </summary>
        public TObject Value { get; }

        public static bool operator ==(Error<TObject, TKey> left, Error<TObject, TKey> right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(Error<TObject, TKey> left, Error<TObject, TKey> right)
        {
            return !Equals(left, right);
        }

        /// <inheritdoc />
        public bool Equals(Error<TObject, TKey>? other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return EqualityComparer<TKey>.Default.Equals(Key, other.Key) && EqualityComparer<TObject>.Default.Equals(Value, other.Value) && Equals(Exception, other.Exception);
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            return obj is Error<TObject, TKey> error && Equals(error);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = Key is null ? 0 : EqualityComparer<TKey>.Default.GetHashCode(Key);
                hashCode = (hashCode * 397) ^ (Value is null ? 0 : EqualityComparer<TObject>.Default.GetHashCode(Value));
                hashCode = (hashCode * 397) ^ (Exception is not null ? Exception.GetHashCode() : 0);
                return hashCode;
            }
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"Key: {Key}, Value: {Value}, Exception: {Exception}";
        }
    }
}