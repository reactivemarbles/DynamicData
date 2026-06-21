// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

// ReSharper disable once CheckNamespace

namespace DynamicData;

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
    /// <param name="source">The <see cref="ISourceCache{TObject, TKey}"/> to operate on.</param>
    /// <param name="observable">The <see cref="IObservable{IEnumerable{TObject}}"/> that emits batches of items.</param>
    /// <returns>An <see cref="IDisposable"/> that, when disposed, unsubscribes from <paramref name="observable"/>.</returns>
    /// <remarks>
    /// <para>Each emission from <paramref name="observable"/> is passed to <see cref="AddOrUpdate{TObject, TKey}(ISourceCache{TObject, TKey}, IEnumerable{TObject})"/>, producing one changeset per emission containing <b>Add</b> or <b>Update</b> events for each item. Errors from <paramref name="observable"/> propagate and terminate the subscription. Completion ends the subscription; the cache retains all items.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="observable"/> is <see langword="null"/>.</exception>
    /// <seealso cref="PopulateInto{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, ISourceCache{TObject, TKey})"/>
    /// <seealso cref="ToObservableChangeSet{TObject, TKey}(IObservable{IEnumerable{TObject}}, Func{TObject, TKey}, Func{TObject, TimeSpan?}, int, IScheduler?)"/>
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
    /// <param name="source">The <see cref="ISourceCache{TObject, TKey}"/> to operate on.</param>
    /// <param name="observable">The <see cref="IObservable{TObject}"/> that emits individual items.</param>
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
