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
    /// Like <c>MergeMany&lt;TObject, TKey, TDestination&gt;(IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;, Func&lt;TObject, IObservable&lt;TDestination&gt;&gt;)</c>,
    /// but wraps each emitted value as an <c>ItemWithValue&lt;TObject, TValue&gt;</c>, pairing the source item
    /// with the value it produced. This lets you identify which source item is responsible for each emission.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the source cache.</typeparam>
    /// <typeparam name="TKey">The type of the key identifying source cache items.</typeparam>
    /// <typeparam name="TDestination">The type of values emitted by child observables.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;</c> whose items each produce an observable.</param>
    /// <param name="observableSelector">A <c>Func&lt;T, TResult&gt;</c> factory function that produces a child observable for each source item.</param>
    /// <returns>An observable of <c>ItemWithValue&lt;TObject, TValue&gt;</c> pairing each emission with its source item.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="observableSelector"/> is null.</exception>
    public static IObservable<ItemWithValue<TObject, TDestination>> MergeManyItems<TObject, TKey, TDestination>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IObservable<TDestination>> observableSelector)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(observableSelector);

        return new MergeManyItems<TObject, TKey, TDestination>(source, observableSelector).Run();
    }

    /// <summary>
    /// Provides an overload of <c>MergeManyItems</c> for the supplied arguments.
    /// </summary>
    /// <typeparam name="TObject">The type of the TObject value.</typeparam>
    /// <typeparam name="TKey">The type of the TKey value.</typeparam>
    /// <typeparam name="TDestination">The type of the TDestination value.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;</c> whose items each produce an observable.</param>
    /// <param name="observableSelector">A <c>Func&lt;T, TResult&gt;</c> factory function that receives both the item and its key, and returns a child observable.</param>
    /// <returns>The resulting observable sequence.</returns>
    public static IObservable<ItemWithValue<TObject, TDestination>> MergeManyItems<TObject, TKey, TDestination>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IObservable<TDestination>> observableSelector)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(observableSelector);

        return new MergeManyItems<TObject, TKey, TDestination>(source, observableSelector).Run();
    }
}
