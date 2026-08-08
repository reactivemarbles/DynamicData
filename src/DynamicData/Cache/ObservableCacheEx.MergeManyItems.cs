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
    /// Like <see cref="MergeMany{TObject, TKey, TDestination}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, IObservable{TDestination}})"/>,
    /// but wraps each emitted value as an <see cref="ItemWithValue{TObject, TValue}"/>, pairing the source item
    /// with the value it produced. This lets you identify which source item is responsible for each emission.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the source cache.</typeparam>
    /// <typeparam name="TKey">The type of the key identifying source cache items.</typeparam>
    /// <typeparam name="TDestination">The type of values emitted by child observables.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> whose items each produce an observable.</param>
    /// <param name="observableSelector">A <see cref="Func{T, TResult}"/> factory function that produces a child observable for each source item.</param>
    /// <returns>An observable of <see cref="ItemWithValue{TObject, TValue}"/> pairing each emission with its source item.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="observableSelector"/> is null.</exception>
    public static IObservable<ItemWithValue<TObject, TDestination>> MergeManyItems<TObject, TKey, TDestination>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IObservable<TDestination>> observableSelector)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        observableSelector.ThrowArgumentNullExceptionIfNull(nameof(observableSelector));

        return new MergeManyItems<TObject, TKey, TDestination>(source, observableSelector).Run();
    }

    /// <inheritdoc cref="MergeManyItems{TObject, TKey, TDestination}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, IObservable{TDestination}})"/>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> whose items each produce an observable.</param>
    /// <param name="observableSelector">A <see cref="Func{T, TResult}"/> factory function that receives both the item and its key, and returns a child observable.</param>
    public static IObservable<ItemWithValue<TObject, TDestination>> MergeManyItems<TObject, TKey, TDestination>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IObservable<TDestination>> observableSelector)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        observableSelector.ThrowArgumentNullExceptionIfNull(nameof(observableSelector));

        return new MergeManyItems<TObject, TKey, TDestination>(source, observableSelector).Run();
    }
}
