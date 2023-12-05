// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

namespace DynamicData.Kernel;

/// <summary>
/// The equivalent of a nullable type which works on value and reference types.
/// </summary>
/// <typeparam name="T">The underlying value type of the <see cref="Nullable{T}"/> generic type.</typeparam><filterpriority>1.</filterpriority>
[SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "Deliberate usage.")]
[SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "Class names the same, generic differences.")]
public readonly struct Optional<T> : IEquatable<Optional<T>>
    where T : notnull
{
    private readonly T? _value;

    /// <summary>
    /// Initializes a new instance of the <see cref="Optional{T}"/> struct.
    /// </summary>
    /// <param name="value">The value.</param>
    internal Optional(T? value)
    {
        if (value is null)
        {
            HasValue = false;
            _value = default;
        }
        else
        {
            _value = value;
            HasValue = true;
        }
    }

    /// <summary>
    /// Gets the default valueless optional.
    /// </summary>
    public static Optional<T> None { get; }

    /// <summary>
    /// Gets a value indicating whether the current <see cref="Nullable{T}"/> object has a value.
    /// </summary>
    ///
    /// <returns>
    /// true if the current <see cref="Nullable{T}"/> object has a value; false if the current <see cref="Nullable{T}"/> object has no value.
    /// </returns>
    public bool HasValue { get; }

    /// <summary>
    /// Gets the value of the current <see cref="Nullable{T}"/> value.
    /// </summary>
    ///
    /// <returns>
    /// The value of the current <see cref="Nullable{T}"/> object if the <see cref="Nullable{T}.HasValue"/> property is true. An exception is thrown if the <see cref="Nullable{T}.HasValue"/> property is false.
    /// </returns>
    /// <exception cref="InvalidOperationException">The <see cref="Nullable{T}.HasValue"/> property is false.</exception>
    public T Value
    {
        get
        {
            if (!HasValue || _value is null)
            {
                throw new InvalidOperationException("Optional<T> has no value");
            }

            return _value;
        }
    }

    /// <summary>
    /// Implicit cast from the vale to the optional.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The optional value.</returns>
    public static implicit operator Optional<T>(T? value) => ToOptional(value);

    /// <summary>
    /// Explicit cast from option to value.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The optional value.</returns>
    public static explicit operator T?(in Optional<T> value) => FromOptional(value);

    public static bool operator ==(in Optional<T> left, in Optional<T> right) => left.Equals(right);

    public static bool operator !=(in Optional<T> left, in Optional<T> right) => !left.Equals(right);

    /// <summary>
    /// Creates the specified value.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The optional value.</returns>
    public static Optional<T> Create(T? value) => new(value);

    /// <summary>
    /// Gets the value from the optional value.
    /// </summary>
    /// <param name="value">The optional value.</param>
    /// <returns>The value.</returns>
    public static T? FromOptional(in Optional<T> value) => value.Value;

    /// <summary>
    /// Gets the optional from a value.
    /// </summary>
    /// <param name="value">The value to get the optional for.</param>
    /// <returns>The optional.</returns>
    public static Optional<T> ToOptional(T? value) => new(value);

    /// <inheritdoc />
    public bool Equals(Optional<T> other)
    {
        if (!HasValue)
        {
            return !other.HasValue;
        }

        if (!other.HasValue)
        {
            return false;
        }

        if (_value is null && other._value is null)
        {
            return true;
        }

        if (_value is null || other._value is null)
        {
            return false;
        }

        return HasValue.Equals(other.HasValue) && EqualityComparer<T>.Default.Equals(_value, other._value);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        if (obj is null)
        {
            return false;
        }

        return obj is Optional<T> optional && Equals(optional);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        unchecked
        {
            if (_value is null)
            {
                return 0;
            }

            return (HasValue.GetHashCode() * 397) ^ EqualityComparer<T>.Default.GetHashCode(_value);
        }
    }

    /// <inheritdoc />
    public override string? ToString()
    {
        if (_value is null)
        {
            return "<None>";
        }

        return !HasValue ? "<None>" : _value.ToString();
    }
}

/// <summary>
/// Optional factory class.
/// </summary>
[SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "By Design.")]
public static class Optional
{
    /// <summary>
    /// Returns an None optional value for the specified type.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <returns>The optional value.</returns>
    public static Optional<T> None<T>()
        where T : notnull
        => Optional<T>.None;

    /// <summary>
    /// Wraps the specified value in an Optional container.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="value">The value.</param>
    /// <returns>The optional value.</returns>
    public static Optional<T> Some<T>(T? value)
        where T : notnull
        => new(value);
}
