// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

using DynamicData.Reactive.Cache.Internal;
#else

using DynamicData.Cache.Internal;
#endif
#if REACTIVE_SHIM
using DynamicData.Reactive.List.Internal;
#else
using DynamicData.List.Internal;
#endif

// ReSharper disable once CheckNamespace
#if REACTIVE_SHIM
namespace DynamicData.Reactive;
#else
namespace DynamicData;
#endif

/// <summary>
/// Extensions for ObservableList.
/// </summary>
public static partial class ObservableListEx
{
    /// <summary>
    /// Executes the Combine operation.
    /// </summary>
    /// <typeparam name="T">The type of the T value.</typeparam>
    /// <param name="sources">The sources value.</param>
    /// <param name="type">The type value.</param>
    /// <returns>The result of the operation.</returns>
    private static IObservable<IChangeSet<T>> Combine<T>(this ICollection<IObservable<IChangeSet<T>>> sources, CombineOperator type)
        where T : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(sources);

        return new Combiner<T>(sources, type).Run();
    }

    /// <summary>
    /// Executes the Combine operation.
    /// </summary>
    /// <typeparam name="T">The type of the T value.</typeparam>
    /// <param name="source">The source value.</param>
    /// <param name="type">The type value.</param>
    /// <param name="others">The others value.</param>
    /// <returns>The result of the operation.</returns>
    private static IObservable<IChangeSet<T>> Combine<T>(this IObservable<IChangeSet<T>> source, CombineOperator type, params IObservable<IChangeSet<T>>[] others)
        where T : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(others);

        if (others.Length == 0)
        {
            throw new ArgumentException("Must be at least one item to combine with", nameof(others));
        }

        var items = source.EnumerateOne().Union(others).ToList();
        return new Combiner<T>(items, type).Run();
    }

    /// <summary>
    /// Executes the Combine operation.
    /// </summary>
    /// <typeparam name="T">The type of the T value.</typeparam>
    /// <param name="sources">The sources value.</param>
    /// <param name="type">The type value.</param>
    /// <returns>The result of the operation.</returns>
    private static IObservable<IChangeSet<T>> Combine<T>(this IObservableList<ISourceList<T>> sources, CombineOperator type)
        where T : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(sources);

        return Observable.Create<IChangeSet<T>>(
            observer =>
            {
                var changesSetList = sources.Connect().Transform(s => s.Connect()).AsObservableList();
                var subscriber = changesSetList.Combine(type).SubscribeSafe(observer);
                return new CompositeDisposable(changesSetList, subscriber);
            });
    }

    /// <summary>
    /// Executes the Combine operation.
    /// </summary>
    /// <typeparam name="T">The type of the T value.</typeparam>
    /// <param name="sources">The sources value.</param>
    /// <param name="type">The type value.</param>
    /// <returns>The result of the operation.</returns>
    private static IObservable<IChangeSet<T>> Combine<T>(this IObservableList<IObservableList<T>> sources, CombineOperator type)
        where T : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(sources);

        return Observable.Create<IChangeSet<T>>(
            observer =>
            {
                var changesSetList = sources.Connect().Transform(s => s.Connect()).AsObservableList();
                var subscriber = changesSetList.Combine(type).SubscribeSafe(observer);
                return new CompositeDisposable(changesSetList, subscriber);
            });
    }

    /// <summary>
    /// Executes the Combine operation.
    /// </summary>
    /// <typeparam name="T">The type of the T value.</typeparam>
    /// <param name="sources">The sources value.</param>
    /// <param name="type">The type value.</param>
    /// <returns>The result of the operation.</returns>
    private static IObservable<IChangeSet<T>> Combine<T>(this IObservableList<IObservable<IChangeSet<T>>> sources, CombineOperator type)
        where T : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(sources);

        return new DynamicCombiner<T>(sources, type).Run();
    }
}
