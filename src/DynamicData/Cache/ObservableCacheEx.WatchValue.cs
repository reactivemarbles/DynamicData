// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
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
    /// Filters the source changeset stream to a single key, emitting the current value each time it changes.
    /// Even emits the value on removal (the removed item's value).
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservableCache{TObject, TKey}"/> to watch a single key in.</param>
    /// <param name="key">The <typeparamref name="TKey"/> key to observe.</param>
    /// <returns>An observable of the item's value whenever it changes for the specified key.</returns>
    /// <remarks>
    /// <para>
    /// Unlike <see cref="ToObservableOptional{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, TKey, IEqualityComparer{TObject}?)"/>,
    /// this does not emit <see cref="Optional.None{T}"/> on removal. It emits the removed item's value instead.
    /// If you need to distinguish presence from absence, use ToObservableOptional.
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>Emits the added item's value.</description></item>
    /// <item><term>Update</term><description>Emits the new value.</description></item>
    /// <item><term>Remove</term><description>Emits the removed item's value (not <c>None</c>; use <see cref="ToObservableOptional{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, TKey, IEqualityComparer{TObject}?)"/> if you need removal detection).</description></item>
    /// <item><term>Refresh</term><description>Emits the current value.</description></item>
    /// </list>
    /// <para><b>Worth noting:</b> No emission occurs if the key is not present at subscription time. Changes to other keys are ignored entirely.</para>
    /// </remarks>
    /// <seealso cref="Watch{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, TKey)"/>
    /// <seealso cref="ToObservableOptional{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, TKey, IEqualityComparer{TObject}?)"/>
    public static IObservable<TObject> WatchValue<TObject, TKey>(this IObservableCache<TObject, TKey> source, TKey key)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source.Watch(key).Select(u => u.Current);
    }

    /// <inheritdoc cref="WatchValue{TObject, TKey}(IObservableCache{TObject, TKey}, TKey)"/>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to watch a single key in.</param>
    /// <param name="key">The <typeparamref name="TKey"/> key to observe.</param>
    /// <remarks>This overload extends <see cref="IObservable{T}">IObservable</see>&lt;<see cref="IChangeSet{TObject, TKey}"/>&gt; instead of <see cref="IObservableCache{TObject, TKey}"/>.</remarks>
    public static IObservable<TObject> WatchValue<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, TKey key)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source.Watch(key).Select(u => u.Current);
    }
}
