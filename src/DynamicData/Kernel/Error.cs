// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Kernel;
#else

namespace DynamicData.Kernel;
#endif

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
[SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "By Design.")]
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

    /// <summary>
    /// Determines whether two <see cref="Error{TObject, TKey}"/> instances are equal.
    /// </summary>
    /// <param name="left">The left error to compare.</param>
    /// <param name="right">The right error to compare.</param>
    /// <returns><see langword="true"/> when the values are equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator ==(Error<TObject, TKey> left, Error<TObject, TKey> right) => Equals(left, right);

    /// <summary>
    /// Determines whether two <see cref="Error{TObject, TKey}"/> instances are not equal.
    /// </summary>
    /// <param name="left">The left error to compare.</param>
    /// <param name="right">The right error to compare.</param>
    /// <returns><see langword="true"/> when the values are not equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(Error<TObject, TKey> left, Error<TObject, TKey> right) => !Equals(left, right);

    /// <inheritdoc />
    /// <param name="other">The other value.</param>
    /// <returns>The result of the operation.</returns>
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
    /// <param name="obj">The obj value.</param>
    /// <returns>The result of the operation.</returns>
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
    /// <returns>The result of the operation.</returns>
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
    /// <returns>The result of the operation.</returns>
    public override string ToString() => $"Key: {Key}, Value: {Value}, Exception: {Exception}";
}
