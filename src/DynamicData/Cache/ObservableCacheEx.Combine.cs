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
    private static IObservable<IChangeSet<TObject, TKey>> Combine<TObject, TKey>(this IObservableList<IObservableCache<TObject, TKey>> source, CombineOperator type)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return Observable.Create<IChangeSet<TObject, TKey>>(
            observer =>
            {
                var connections = source.Connect().Transform(x => x.Connect()).AsObservableList();
                var subscriber = connections.Combine(type).SubscribeSafe(observer);
                return new CompositeDisposable(connections, subscriber);
            });
    }

    private static IObservable<IChangeSet<TObject, TKey>> Combine<TObject, TKey>(this IObservableList<ISourceCache<TObject, TKey>> source, CombineOperator type)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return Observable.Create<IChangeSet<TObject, TKey>>(
            observer =>
            {
                var connections = source.Connect().Transform(x => x.Connect()).AsObservableList();
                var subscriber = connections.Combine(type).SubscribeSafe(observer);
                return new CompositeDisposable(connections, subscriber);
            });
    }

    private static IObservable<IChangeSet<TObject, TKey>> Combine<TObject, TKey>(this IObservableList<IObservable<IChangeSet<TObject, TKey>>> source, CombineOperator type)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return new DynamicCombiner<TObject, TKey>(source, type).Run();
    }

    private static IObservable<IChangeSet<TObject, TKey>> Combine<TObject, TKey>(this ICollection<IObservable<IChangeSet<TObject, TKey>>> sources, CombineOperator type)
        where TObject : notnull
        where TKey : notnull
    {
        sources.ThrowArgumentNullExceptionIfNull(nameof(sources));

        return Observable.Create<IChangeSet<TObject, TKey>>(
            observer =>
            {
                void UpdateAction(IChangeSet<TObject, TKey> updates)
                {
                    try
                    {
                        observer.OnNext(updates);
                    }
                    catch (Exception ex)
                    {
                        observer.OnError(ex);
                    }
                }

                var subscriber = Disposable.Empty;
                try
                {
                    var combiner = new Combiner<TObject, TKey>(type, UpdateAction);
                    subscriber = combiner.Subscribe([.. sources]);
                }
                catch (Exception ex)
                {
                    observer.OnError(ex);
                    observer.OnCompleted();
                }

                return subscriber;
            });
    }

    private static IObservable<IChangeSet<TObject, TKey>> Combine<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, CombineOperator type, params IObservable<IChangeSet<TObject, TKey>>[] combineTarget)
        where TObject : notnull
        where TKey : notnull
    {
        combineTarget.ThrowArgumentNullExceptionIfNull(nameof(combineTarget));

        return Observable.Create<IChangeSet<TObject, TKey>>(
            observer =>
            {
                void UpdateAction(IChangeSet<TObject, TKey> updates)
                {
                    try
                    {
                        observer.OnNext(updates);
                    }
                    catch (Exception ex)
                    {
                        observer.OnError(ex);
                        observer.OnCompleted();
                    }
                }

                var subscriber = Disposable.Empty;
                try
                {
                    var list = combineTarget.ToList();
                    list.Insert(0, source);

                    var combiner = new Combiner<TObject, TKey>(type, UpdateAction);
                    subscriber = combiner.Subscribe([.. list]);
                }
                catch (Exception ex)
                {
                    observer.OnError(ex);
                    observer.OnCompleted();
                }

                return subscriber;
            });
    }
}
