// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using DynamicData.Binding;
using DynamicData.Cache;
using DynamicData.Cache.Internal;

// ReSharper disable once CheckNamespace

namespace DynamicData;

/// <summary>
/// Extensions for dynamic data.
/// </summary>
public static partial class ObservableCacheEx
{
    /// <summary>
    /// Emits <see langword="true"/> when any item in the cache satisfies a condition based on its per-item observable,
    /// and <see langword="false"/> when none do. Re-evaluates whenever the cache changes or any per-item observable emits.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TValue">The type of the value emitted by each per-item observable.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to evaluate a condition across any item in.</param>
    /// <param name="observableSelector">A <see cref="Func{T, TResult}"/> factory that produces a condition observable for each item.</param>
    /// <param name="equalityCondition">A <see cref="Func{T, TResult}"/> that predicate applied to each item and its per-item observable's latest value.</param>
    /// <returns>An observable of <c>bool</c> that emits whenever the any-item condition changes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/>, <paramref name="observableSelector"/>, or <paramref name="equalityCondition"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>A new per-item subscription is created. The aggregate condition is recalculated.</description></item>
    /// <item><term>Update</term><description>The item is replaced in the collection snapshot. Condition recalculated.</description></item>
    /// <item><term>Remove</term><description>Per-item subscription disposed. Condition recalculated over remaining items.</description></item>
    /// <item><term>Refresh</term><description>No effect on per-item subscriptions. Condition not recalculated unless the per-item observable emits.</description></item>
    /// </list>
    /// <para><b>Worth noting:</b> Items whose per-item observable has not yet emitted are treated as not satisfying the condition. An empty cache yields <see langword="false"/>. The result uses <c>DistinctUntilChanged</c>, so duplicate <c>bool</c> values are suppressed.</para>
    /// </remarks>
    /// <seealso cref="TrueForAll{TObject, TKey, TValue}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, IObservable{TValue}}, Func{TValue, bool})"/>
    public static IObservable<bool> TrueForAny<TObject, TKey, TValue>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IObservable<TValue>> observableSelector, Func<TObject, TValue, bool> equalityCondition)
        where TObject : notnull
        where TKey : notnull
        where TValue : notnull => source.TrueFor(observableSelector, items => items.Any(o => o.LatestValue.HasValue && equalityCondition(o.Item, o.LatestValue.Value)));

    /// <inheritdoc cref="TrueForAny{TObject, TKey, TValue}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, IObservable{TValue}}, Func{TObject, TValue, bool})"/>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to evaluate a condition across any item in.</param>
    /// <param name="observableSelector">A <see cref="Func{T, TResult}"/> factory that produces a condition observable for each item.</param>
    /// <param name="equalityCondition">A <see cref="Func{T, TResult}"/> that predicate applied to each per-item observable's latest value (without the item).</param>
    /// <remarks>This overload accepts a predicate that takes only the value, not the item. Useful when the condition depends only on the observed value.</remarks>
    public static IObservable<bool> TrueForAny<TObject, TKey, TValue>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IObservable<TValue>> observableSelector, Func<TValue, bool> equalityCondition)
        where TObject : notnull
        where TKey : notnull
        where TValue : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        observableSelector.ThrowArgumentNullExceptionIfNull(nameof(observableSelector));
        equalityCondition.ThrowArgumentNullExceptionIfNull(nameof(equalityCondition));

        return source.TrueFor(observableSelector, items => items.Any(o => o.LatestValue.HasValue && equalityCondition(o.LatestValue.Value)));
    }
}
