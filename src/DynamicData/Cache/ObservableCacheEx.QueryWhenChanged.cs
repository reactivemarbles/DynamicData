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
    /// Projects the current cache state through <paramref name="resultSelector"/> after each modification.
    /// Emits a new value of <typeparamref name="TDestination"/> on every changeset.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TDestination">The type of the destination.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to project on each change.</param>
    /// <param name="resultSelector">A function that projects the current <see cref="IQuery{TObject, TKey}"/> snapshot to a result value.</param>
    /// <returns>An observable that emits a projected value after each changeset.</returns>
    /// <remarks>
    /// <para><b>Worth noting:</b> The selector is called on every changeset, which can be chatty. The <see cref="IQuery{TObject, TKey}"/> exposes the full cache state for LINQ-style queries.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="resultSelector"/> is <see langword="null"/>.</exception>
    /// <seealso cref="ToCollection{TObject, TKey}"/>
    /// <seealso cref="ToSortedCollection{TObject, TKey, TSortKey}(IObservable{IChangeSet{TObject, TKey}}, Func{TObject, TSortKey}, SortDirection)"/>
    /// <seealso cref="ObservableListEx.QueryWhenChanged"/>
    public static IObservable<TDestination> QueryWhenChanged<TObject, TKey, TDestination>(this IObservable<IChangeSet<TObject, TKey>> source, Func<IQuery<TObject, TKey>, TDestination> resultSelector)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        resultSelector.ThrowArgumentNullExceptionIfNull(nameof(resultSelector));

        return source.QueryWhenChanged().Select(resultSelector);
    }

    /// <summary>
    /// The latest copy of the cache is exposed for querying i)  after each modification to the underlying data ii) upon subscription.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to project on each change.</param>
    /// <returns>An observable which emits the query.</returns>
    /// <exception cref="ArgumentNullException">source.</exception>
    public static IObservable<IQuery<TObject, TKey>> QueryWhenChanged<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return new QueryWhenChanged<TObject, TKey, Unit>(source).Run();
    }

    /// <summary>
    /// The latest copy of the cache is exposed for querying i)  after each modification to the underlying data ii) on subscription.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to project on each change.</param>
    /// <param name="itemChangedTrigger">A <see cref="Func{T, TResult}"/> that should the query be triggered for observables on individual items.</param>
    /// <returns>An observable that emits the query.</returns>
    /// <exception cref="ArgumentNullException">source.</exception>
    public static IObservable<IQuery<TObject, TKey>> QueryWhenChanged<TObject, TKey, TValue>(this IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IObservable<TValue>> itemChangedTrigger)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        itemChangedTrigger.ThrowArgumentNullExceptionIfNull(nameof(itemChangedTrigger));

        return new QueryWhenChanged<TObject, TKey, TValue>(source, itemChangedTrigger).Run();
    }
}
