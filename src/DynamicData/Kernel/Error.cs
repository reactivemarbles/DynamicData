// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Kernel;

/// <summary>
/// An error container used to report errors from within dynamic data operators.
/// </summary>
/// <typeparam name="TObject">The type of the object.</typeparam>
/// <typeparam name="TKey">The type of the key.</typeparam>
/// <remarks>
/// Initializes a new instance of the <see cref="Error{TObject, TKey}"/> class.
/// </remarks>
/// <param name="exception">The exception that caused the error.</param>
/// <param name="value">The value for the error.</param>
/// <param name="key">The key for the error.</param>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "By Design.")]
public sealed class Error<TObject, TKey>(Exception? exception, TObject value, TKey key) : IKeyValue<TObject, TKey>, IEquatable<Error<TObject, TKey>>
    where TKey : notnull
{
    /// <summary>
    /// Gets the exception.
    /// </summary>
    public Exception? Exception { get; } = exception;

    /// <summary>
    /// Gets the key.
    /// </summary>
    public TKey Key { get; } = key;

    /// <summary>
    /// Gets the object.
    /// </summary>
    public TObject Value { get; } = value;

    public static bool operator ==(Error<TObject, TKey> left, Error<TObject, TKey> right) => Equals(left, right);

    public static bool operator !=(Error<TObject, TKey> left, Error<TObject, TKey> right) => !Equals(left, right);

    /// <inheritdoc />
    public bool Equals(Error<TObject, TKey>? other)
    {
        if (other is null)
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
        if (obj is null)
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
            var hashCode = EqualityComparer<TKey>.Default.GetHashCode(Key);
            hashCode = (hashCode * 397) ^ (Value is null ? 0 : EqualityComparer<TObject>.Default.GetHashCode(Value));
            hashCode = (hashCode * 397) ^ (Exception?.GetHashCode() ?? 0);
            return hashCode;
        }
    }

    /// <inheritdoc />
    public override string ToString() => $"Key: {Key}, Value: {Value}, Exception: {Exception}";
}
