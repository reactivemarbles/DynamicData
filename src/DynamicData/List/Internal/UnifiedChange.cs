// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.List.Internal;
#else

namespace DynamicData.List.Internal;
#endif

/// <summary>
/// Represents the UnifiedChange value.
/// </summary>
/// <typeparam name="T">The type of the T value.</typeparam>
/// <param name="reason">The reason value.</param>
/// <param name="current">The current value.</param>
/// <param name="previous">The previous value.</param>
internal readonly struct UnifiedChange<T>(ListChangeReason reason, T current, ReactiveUI.Primitives.Optional<T> previous) : IEquatable<UnifiedChange<T>>
    where T : notnull
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UnifiedChange{T}"/> struct.
    /// </summary>
    /// <param name="reason">The reason value.</param>
    /// <param name="current">The current value.</param>
    public UnifiedChange(ListChangeReason reason, T current)
        : this(reason, current, ReactiveUI.Primitives.Optional<T>.None)
    {
    }

    /// <summary>
    /// Gets the Reason value.
    /// </summary>
    public ListChangeReason Reason { get; } = reason;

    /// <summary>
    /// Gets the Current value.
    /// </summary>
    public T Current { get; } = current;

    /// <summary>
    /// Gets the Previous value.
    /// </summary>
    public ReactiveUI.Primitives.Optional<T> Previous { get; } = previous;

    /// <summary>
    /// Executes the operator operation.
    /// </summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>The result of the operation.</returns>
    public static bool operator ==(in UnifiedChange<T> left, in UnifiedChange<T> right) => left.Equals(right);

    /// <summary>
    /// Executes the operator operation.
    /// </summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>The result of the operation.</returns>
    public static bool operator !=(in UnifiedChange<T> left, in UnifiedChange<T> right) => !left.Equals(right);

    /// <summary>
    /// Executes the Equals operation.
    /// </summary>
    /// <param name="other">The other value.</param>
    /// <returns>The result of the operation.</returns>
    public bool Equals(UnifiedChange<T> other) => Reason == other.Reason && EqualityComparer<T>.Default.Equals(Current, other.Current) && Previous.Equals(other.Previous);

    /// <summary>
    /// Executes the Equals operation.
    /// </summary>
    /// <param name="obj">The obj value.</param>
    /// <returns>The result of the operation.</returns>
    public override bool Equals(object? obj)
    {
        if (obj is null)
        {
            return false;
        }

        return obj is UnifiedChange<T> unifiedChange && Equals(unifiedChange);
    }

    /// <summary>
    /// Executes the GetHashCode operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = (int)Reason;
            hashCode = (hashCode * 397) ^ (Current is null ? 0 : EqualityComparer<T>.Default.GetHashCode(Current));
            hashCode = (hashCode * 397) ^ Previous.GetHashCode();
            return hashCode;
        }
    }

    /// <summary>
    /// Executes the ToString operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public override string ToString() => $"Reason: {Reason}, Current: {Current}, Previous: {Previous}";
}
