// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

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
    /// Subscribes to the observable and calls <c>AddOrUpdate</c> on the source cache for each emitted batch of items.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The <c>ISourceCache&lt;TObject, TKey&gt;</c> to operate on.</param>
    /// <param name="observable">The <c>IObservable&lt;IEnumerable&lt;TObject&gt;&gt;</c> that emits batches of items.</param>
    /// <returns>An <see cref="IDisposable"/> that, when disposed, unsubscribes from <paramref name="observable"/>.</returns>
    /// <remarks>
    /// <para>Each emission from <paramref name="observable"/> is passed to <c>AddOrUpdate&lt;TObject, TKey&gt;(ISourceCache&lt;TObject, TKey&gt;, IEnumerable&lt;TObject&gt;)</c>, producing one changeset per emission containing <b>Add</b> or <b>Update</b> events for each item. Errors from <paramref name="observable"/> propagate and terminate the subscription. Completion ends the subscription; the cache retains all items.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="observable"/> is <see langword="null"/>.</exception>
    /// <seealso><c>PopulateInto&lt;TObject, TKey&gt;(IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;, ISourceCache&lt;TObject, TKey&gt;)</c></seealso>
    /// <seealso><c>ToObservableChangeSet&lt;TObject, TKey&gt;(IObservable&lt;IEnumerable&lt;TObject&gt;&gt;, Func&lt;TObject, TKey&gt;, Func&lt;TObject, TimeSpan?&gt;, int, IScheduler?)</c></seealso>
    public static IDisposable PopulateFrom<TObject, TKey>(this ISourceCache<TObject, TKey> source, IObservable<IEnumerable<TObject>> observable)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(observable);

        return observable.Subscribe(source.AddOrUpdate);
    }

    /// <summary>
    /// Subscribes to the observable and calls <c>AddOrUpdate</c> on the source cache for each emitted item.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The <c>ISourceCache&lt;TObject, TKey&gt;</c> to operate on.</param>
    /// <param name="observable">The <c>IObservable&lt;TObject&gt;</c> that emits individual items.</param>
    /// <returns>An <see cref="IDisposable"/> that, when disposed, unsubscribes from <paramref name="observable"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="observable"/> is <see langword="null"/>.</exception>
    public static IDisposable PopulateFrom<TObject, TKey>(this ISourceCache<TObject, TKey> source, IObservable<TObject> observable)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(observable);

        return observable.Subscribe(source.AddOrUpdate);
    }
}
