// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using DynamicData.List.Internal;

// ReSharper disable once CheckNamespace
namespace DynamicData;

/// <summary>
/// Extensions for ObservableList.
/// </summary>
public static partial class ObservableListEx
{
    /// <summary>
    /// Extracts distinct values from source items using <paramref name="valueSelector"/>, with reference counting to track when values enter and leave the result set.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the source list.</typeparam>
    /// <typeparam name="TValue">The type of distinct values produced.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject}}"/> to extract distinct values.</param>
    /// <param name="valueSelector">A <see cref="Func{T, TResult}"/> function that extracts the value to track from each source item.</param>
    /// <returns>A list changeset stream of distinct values.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="valueSelector"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// Maintains an internal reference count per distinct value. A value is included when its count first exceeds zero
    /// and removed when its count drops back to zero.
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term><b>Add</b>/<b>AddRange</b></term><description>Value extracted. If first occurrence, an <b>Add</b> is emitted. Otherwise the reference count is incremented silently.</description></item>
    /// <item><term><b>Replace</b></term><description>Old value's reference count decremented (removed if zero), new value's count incremented (added if first). If the value did not change, no emission.</description></item>
    /// <item><term><b>Remove</b>/<b>RemoveRange</b></term><description>Reference count decremented. If the count reaches zero, a <b>Remove</b> is emitted for that distinct value.</description></item>
    /// <item><term><b>Refresh</b></term><description>Value is re-extracted. If changed, old value decremented and new value incremented (same as Replace logic).</description></item>
    /// <item><term><b>Clear</b></term><description>All reference counts cleared. <b>Remove</b> emitted for every tracked distinct value.</description></item>
    /// </list>
    /// </remarks>
    /// <seealso cref="ObservableCacheEx.DistinctValues{TObject, TKey, TValue}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, TValue})"/>
    public static IObservable<IChangeSet<TValue>> DistinctValues<TObject, TValue>(this IObservable<IChangeSet<TObject>> source, Func<TObject, TValue> valueSelector)
        where TObject : notnull
        where TValue : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(valueSelector);

        return new Distinct<TObject, TValue>(source, valueSelector).Run();
    }
}
