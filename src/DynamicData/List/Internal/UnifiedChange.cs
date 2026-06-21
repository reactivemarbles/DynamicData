// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.List.Internal;
#else

namespace DynamicData.List.Internal;
#endif

internal readonly struct UnifiedChange<T>(ListChangeReason reason, T current, ReactiveUI.Primitives.Optional<T> previous) : IEquatable<UnifiedChange<T>>
    where T : notnull
{
    public UnifiedChange(ListChangeReason reason, T current)
        : this(reason, current, ReactiveUI.Primitives.Optional<T>.None)
    {
    }

    public ListChangeReason Reason { get; } = reason;

    public T Current { get; } = current;

    public ReactiveUI.Primitives.Optional<T> Previous { get; } = previous;

    public static bool operator ==(in UnifiedChange<T> left, in UnifiedChange<T> right) => left.Equals(right);

    public static bool operator !=(in UnifiedChange<T> left, in UnifiedChange<T> right) => !left.Equals(right);

    public bool Equals(UnifiedChange<T> other) => Reason == other.Reason && EqualityComparer<T>.Default.Equals(Current, other.Current) && Previous.Equals(other.Previous);

    public override bool Equals(object? obj)
    {
        if (obj is null)
        {
            return false;
        }

        return obj is UnifiedChange<T> unifiedChange && Equals(unifiedChange);
    }

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

    public override string ToString() => $"Reason: {Reason}, Current: {Current}, Previous: {Previous}";
}
