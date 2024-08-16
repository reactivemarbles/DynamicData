// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive;
using System.Reactive.Linq;

using DynamicData.Kernel;

namespace DynamicData.Cache.Internal;

internal sealed class TransformImmutable<TDestination, TSource, TKey>
    where TDestination : notnull
    where TSource : notnull
    where TKey : notnull
{
    private readonly IObservable<IChangeSet<TSource, TKey>> _source;
    private readonly Func<TSource, TDestination> _transformFactory;

    public TransformImmutable(
        IObservable<IChangeSet<TSource, TKey>> source,
        Func<TSource, TDestination> transformFactory)
    {
        _source = source;
        _transformFactory = transformFactory;
    }

    public IObservable<IChangeSet<TDestination, TKey>> Run()
        => Observable.Create<IChangeSet<TDestination, TKey>>(observer => _source
            .SubscribeSafe(Observer.Create<IChangeSet<TSource, TKey>>(
                onNext: upstreamChanges =>
                {
                    var downstreamChanges = new ChangeSet<TDestination, TKey>(capacity: upstreamChanges.Count);

                    try
                    {
                        foreach (var change in upstreamChanges.ToConcreteType())
                        {
                            downstreamChanges.Add(new(
                                reason: change.Reason,
                                key: change.Key,
                                current: _transformFactory.Invoke(change.Current),
                                previous: change.Previous.HasValue
                                    ? _transformFactory.Invoke(change.Previous.Value)
                                    : Optional.None<TDestination>(),
                                currentIndex: change.CurrentIndex,
                                previousIndex: change.PreviousIndex));
                        }
                    }

                    // We're invoking "untrusted" consumer code, (I.E. _transformFactory), so catch and propagate any errors
                    catch (Exception error)
                    {
                        observer.OnError(error);
                    }

                    observer.OnNext(downstreamChanges);
                },
                onError: observer.OnError,
                onCompleted: observer.OnCompleted)));
}
