// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using DynamicData.Cache.Internal;
using DynamicData.List.Internal;

// ReSharper disable once CheckNamespace
namespace DynamicData;

/// <summary>
/// Extensions for ObservableList.
/// </summary>
public static partial class ObservableListEx
{
    private static IObservable<IChangeSet<T>> Combine<T>(this ICollection<IObservable<IChangeSet<T>>> sources, CombineOperator type)
        where T : notnull
    {
        sources.ThrowArgumentNullExceptionIfNull(nameof(sources));

        return new Combiner<T>(sources, type).Run();
    }

    private static IObservable<IChangeSet<T>> Combine<T>(this IObservable<IChangeSet<T>> source, CombineOperator type, params IObservable<IChangeSet<T>>[] others)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        others.ThrowArgumentNullExceptionIfNull(nameof(others));

        if (others.Length == 0)
        {
            throw new ArgumentException("Must be at least one item to combine with", nameof(others));
        }

        var items = source.EnumerateOne().Union(others).ToList();
        return new Combiner<T>(items, type).Run();
    }

    private static IObservable<IChangeSet<T>> Combine<T>(this IObservableList<ISourceList<T>> sources, CombineOperator type)
        where T : notnull
    {
        sources.ThrowArgumentNullExceptionIfNull(nameof(sources));

        return Observable.Create<IChangeSet<T>>(
            observer =>
            {
                var changesSetList = sources.Connect().Transform(s => s.Connect()).AsObservableList();
                var subscriber = changesSetList.Combine(type).SubscribeSafe(observer);
                return new CompositeDisposable(changesSetList, subscriber);
            });
    }

    private static IObservable<IChangeSet<T>> Combine<T>(this IObservableList<IObservableList<T>> sources, CombineOperator type)
        where T : notnull
    {
        sources.ThrowArgumentNullExceptionIfNull(nameof(sources));

        return Observable.Create<IChangeSet<T>>(
            observer =>
            {
                var changesSetList = sources.Connect().Transform(s => s.Connect()).AsObservableList();
                var subscriber = changesSetList.Combine(type).SubscribeSafe(observer);
                return new CompositeDisposable(changesSetList, subscriber);
            });
    }

    private static IObservable<IChangeSet<T>> Combine<T>(this IObservableList<IObservable<IChangeSet<T>>> sources, CombineOperator type)
        where T : notnull
    {
        sources.ThrowArgumentNullExceptionIfNull(nameof(sources));

        return new DynamicCombiner<T>(sources, type).Run();
    }
}
