// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

using DynamicData.Reactive.Cache.Internal;
#else

using DynamicData.Cache.Internal;
#endif

// ReSharper disable once CheckNamespace
#if REACTIVE_SHIM

namespace DynamicData.Reactive;
#else

namespace DynamicData;
#endif

/// <summary>
/// Extensions for dynamic data.
/// </summary>
public static partial class ObservableCacheEx
{
    /// <summary>
    /// Watches a single key in the source changeset stream, emitting <c>ReactiveUI.Primitives.Optional.Some(value)</c> when the key
    /// is present and <c>ReactiveUI.Primitives.Optional.None&lt;T&gt;</c> when it is removed. Duplicate values are suppressed via <paramref name="equalityComparer"/>.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;</c> to watch a single key in.</param>
    /// <param name="key">The <typeparamref name="TKey"/> key to watch.</param>
    /// <param name="equalityComparer">An <c>IEqualityComparer&lt;TObject&gt;</c> that optional comparer to suppress duplicate emissions. Uses default equality if <see langword="null"/>.</param>
    /// <returns>An observable of <c>Optional&lt;TObject&gt;</c> that reflects the presence or absence of the specified key.</returns>
    /// <remarks>
    /// <para>
    /// Unlike <c>WatchValue&lt;TObject, TKey&gt;(IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;, TKey)</c>, this emits <c>None</c> on removal
    /// (rather than the removed value), making it possible to distinguish "key is absent" from "key has a value".
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>Emits <c>ReactiveUI.Primitives.Optional.Some(value)</c> if the key was not previously tracked.</description></item>
    /// <item><term>Update</term><description>Emits <c>ReactiveUI.Primitives.Optional.Some(newValue)</c> if the new value differs from the previous per <paramref name="equalityComparer"/>. Otherwise suppressed.</description></item>
    /// <item><term>Remove</term><description>Emits <c>ReactiveUI.Primitives.Optional.None&lt;T&gt;</c>.</description></item>
    /// <item><term>Refresh</term><description>Emits <c>ReactiveUI.Primitives.Optional.Some(value)</c> if the value differs from the last emission per <paramref name="equalityComparer"/>. Otherwise suppressed.</description></item>
    /// </list>
    /// <para><b>Worth noting:</b> No emission occurs if the key is not present at subscription time. To get an initial <c>None</c> when the key is absent, use the overload with <c>initialOptionalWhenMissing: true</c>.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <seealso><c>Watch&lt;TObject, TKey&gt;(IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;, TKey)</c></seealso>
    /// <seealso><c>WatchValue&lt;TObject, TKey&gt;(IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;, TKey)</c></seealso>
    public static IObservable<ReactiveUI.Primitives.Optional<TObject>> ToObservableOptional<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, TKey key, IEqualityComparer<TObject>? equalityComparer = null)
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
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;</c> to watch a single key in.</param>
    /// <param name="key">The <typeparamref name="TKey"/> key value.</param>
    /// <param name="initialOptionalWhenMissing">When <see langword="true"/>, emits an initial <c>Optional&lt;TObject&gt;</c> with no value if the key is not present in the cache.</param>
    /// <param name="equalityComparer">An optional <c>IEqualityComparer&lt;TObject&gt;</c> instance used to determine if an object value has changed.</param>
    /// <returns>An observable optional.</returns>
    /// <exception cref="ArgumentNullException">source is null.</exception>
    /// <remarks>
    /// <para><b>Worth noting:</b> The initial <c>None</c> is emitted only if the source subscription has not already synchronously produced a value for the key.</para>
    /// </remarks>
    public static IObservable<ReactiveUI.Primitives.Optional<TObject>> ToObservableOptional<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, TKey key, bool initialOptionalWhenMissing, IEqualityComparer<TObject>? equalityComparer = null)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);

        if (initialOptionalWhenMissing)
        {
            return Observable.Create<ReactiveUI.Primitives.Optional<TObject>>(observer =>
            {
                var queue = new DeliveryQueue<ReactiveUI.Primitives.Optional<TObject>>(observer);
                var hasEmitted = false;
                var subscription = source.ToObservableOptional(key, equalityComparer).Subscribe(
                    value =>
                    {
                        using var scope = queue.AcquireLock();
                        hasEmitted = true;
                        scope.EnqueueNext(value);
                    },
                    error =>
                    {
                        using var scope = queue.AcquireLock();
                        scope.EnqueueError(error);
                    },
                    () =>
                    {
                        using var scope = queue.AcquireLock();
                        if (!hasEmitted)
                        {
                            hasEmitted = true;
                            scope.EnqueueNext(ReactiveUI.Primitives.Optional<TObject>.None);
                        }

                        scope.EnqueueCompleted();
                    });

                using (var scope = queue.AcquireLock())
                {
                    if (!hasEmitted)
                    {
                        hasEmitted = true;
                        scope.EnqueueNext(ReactiveUI.Primitives.Optional<TObject>.None);
                    }
                }

                return Disposable.Create(() =>
                {
                    queue.Dispose();
                    subscription.Dispose();
                });
            });
        }

        return source.ToObservableOptional(key, equalityComparer);
    }
}
