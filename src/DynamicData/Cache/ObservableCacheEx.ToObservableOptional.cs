// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using DynamicData.Cache.Internal;

// ReSharper disable once CheckNamespace

namespace DynamicData;

/// <summary>
/// Extensions for dynamic data.
/// </summary>
public static partial class ObservableCacheEx
{
    /// <summary>
    /// Watches a single key in the source changeset stream, emitting <c>Optional.Some(value)</c> when the key
    /// is present and <see cref="Optional.None{T}"/> when it is removed. Duplicate values are suppressed via <paramref name="equalityComparer"/>.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to watch a single key in.</param>
    /// <param name="key">The <typeparamref name="TKey"/> key to watch.</param>
    /// <param name="equalityComparer">An <see cref="IEqualityComparer{TObject}"/> that optional comparer to suppress duplicate emissions. Uses default equality if <see langword="null"/>.</param>
    /// <returns>An observable of <see cref="Optional{TObject}"/> that reflects the presence or absence of the specified key.</returns>
    /// <remarks>
    /// <para>
    /// Unlike <see cref="WatchValue{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, TKey)"/>, this emits <c>None</c> on removal
    /// (rather than the removed value), making it possible to distinguish "key is absent" from "key has a value".
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>Emits <c>Optional.Some(value)</c> if the key was not previously tracked.</description></item>
    /// <item><term>Update</term><description>Emits <c>Optional.Some(newValue)</c> if the new value differs from the previous per <paramref name="equalityComparer"/>. Otherwise suppressed.</description></item>
    /// <item><term>Remove</term><description>Emits <see cref="Optional.None{T}"/>.</description></item>
    /// <item><term>Refresh</term><description>Emits <c>Optional.Some(value)</c> if the value differs from the last emission per <paramref name="equalityComparer"/>. Otherwise suppressed.</description></item>
    /// </list>
    /// <para><b>Worth noting:</b> No emission occurs if the key is not present at subscription time. To get an initial <c>None</c> when the key is absent, use the overload with <c>initialOptionalWhenMissing: true</c>.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <seealso cref="Watch{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, TKey)"/>
    /// <seealso cref="WatchValue{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, TKey)"/>
    public static IObservable<Optional<TObject>> ToObservableOptional<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, TKey key, IEqualityComparer<TObject>? equalityComparer = null)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);

        return new ToObservableOptional<TObject, TKey>(source, key, equalityComparer).Run();
    }

    /// <summary>
    /// Converts an observable cache into an observable optional that emits the value for the given key.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to watch a single key in.</param>
    /// <param name="key">The <typeparamref name="TKey"/> key value.</param>
    /// <param name="initialOptionalWhenMissing">When <see langword="true"/>, emits an initial <see cref="Optional{TObject}"/> with no value if the key is not present in the cache.</param>
    /// <param name="equalityComparer">An optional <see cref="IEqualityComparer{TObject}"/> instance used to determine if an object value has changed.</param>
    /// <returns>An observable optional.</returns>
    /// <exception cref="ArgumentNullException">source is null.</exception>
    /// <remarks>
    /// <para><b>Worth noting:</b> Uses lock-based coordination. If the key exists synchronously on <c>Connect()</c>, the initial <c>None</c> may or may not be emitted depending on timing.</para>
    /// </remarks>
    public static IObservable<Optional<TObject>> ToObservableOptional<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, TKey key, bool initialOptionalWhenMissing, IEqualityComparer<TObject>? equalityComparer = null)
        where TObject : notnull
        where TKey : notnull
    {
        if (initialOptionalWhenMissing)
        {
            return Observable.Defer(() =>
            {
                var seenValue = false;
                return source.ToObservableOptional(key, equalityComparer)
                    .Do(_ => seenValue = true)
                    .Merge(Observable.Defer(() => seenValue
                        ? Observable.Empty<Optional<TObject>>()
                        : Observable.Return(Optional<TObject>.None)));
            });
        }

        return source.ToObservableOptional(key, equalityComparer);
    }
}
