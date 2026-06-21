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
    /// Dynamically apply a logical Except operator between the collections
    /// Items from the first collection in the outer list are included unless contained in any of the other lists.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;</c> to combine.</param>
    /// <param name="others">The additional <c>IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;</c> streams to combine with.</param>
    /// <returns>An observable which emits change sets.</returns>
    /// <exception cref="ArgumentNullException">
    /// source
    /// or
    /// others.
    /// </exception>
    /// <seealso><c>ObservableListEx.Except</c></seealso>
    public static IObservable<IChangeSet<TObject, TKey>> Except<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, params IObservable<IChangeSet<TObject, TKey>>[] others)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);

        if (others is null || others.Length == 0)
        {
            throw new ArgumentNullException(nameof(others));
        }

        return source.Combine(CombineOperator.Except, others);
    }

    /// <summary>
    /// Dynamically apply a logical Except operator between the collections
    /// Items from the first collection in the outer list are included unless contained in any of the other lists.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="sources">The <c>ICollection&lt;T&gt;</c> of streams to combine.</param>
    /// <returns>An observable which emits change sets.</returns>
    /// <exception cref="ArgumentNullException">
    /// source
    /// or
    /// others.
    /// </exception>
    public static IObservable<IChangeSet<TObject, TKey>> Except<TObject, TKey>(this ICollection<IObservable<IChangeSet<TObject, TKey>>> sources)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(sources);

        return sources.Combine(CombineOperator.Except);
    }

    /// <summary>
    /// Dynamically apply a logical Except operator between the collections
    /// Items from the first collection in the outer list are included unless contained in any of the other lists.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="sources">The <c>IObservableList&lt;T&gt;</c> of streams to combine.</param>
    /// <returns>An observable which emits change sets.</returns>
    public static IObservable<IChangeSet<TObject, TKey>> Except<TObject, TKey>(this IObservableList<IObservable<IChangeSet<TObject, TKey>>> sources)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(sources);

        return sources.Combine(CombineOperator.Except);
    }

    /// <summary>
    /// Dynamically apply a logical Except operator between the items in the outer observable list.
    /// Items which are in any of the sources are included in the result.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="sources">The <c>IObservableList&lt;IObservableCache&lt;TObject, TKey&gt;&gt;</c> of changeset streams to combine.</param>
    /// <returns>An observable which emits change sets.</returns>
    public static IObservable<IChangeSet<TObject, TKey>> Except<TObject, TKey>(this IObservableList<IObservableCache<TObject, TKey>> sources)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(sources);

        return sources.Combine(CombineOperator.Except);
    }

    /// <summary>
    /// Dynamically apply a logical Except operator between the items in the outer observable list.
    /// Items which are in any of the sources are included in the result.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="sources">The <c>IObservableList&lt;ISourceCache&lt;TObject, TKey&gt;&gt;</c> of changeset streams to combine.</param>
    /// <returns>An observable which emits change sets.</returns>
    public static IObservable<IChangeSet<TObject, TKey>> Except<TObject, TKey>(this IObservableList<ISourceCache<TObject, TKey>> sources)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(sources);

        return sources.Combine(CombineOperator.Except);
    }
}
