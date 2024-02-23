// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using DynamicData.Internal;
using DynamicData.Kernel;

namespace DynamicData.Cache.Internal;

internal sealed class OfType<TObject, TKey, TDestination>(IObservable<IChangeSet<TObject, TKey>> source, bool suppressEmptyChangeSets)
    where TObject : notnull
    where TKey : notnull
    where TDestination : notnull
{
    public IObservable<IChangeSet<TDestination, TKey>> Run()
        => Observable.Create<IChangeSet<TDestination, TKey>>(observer => source
            .SubscribeSafe(
                onNext: upstreamChanges =>
                {
                    var downstreamChanges = new ChangeSet<TDestination, TKey>(capacity: upstreamChanges.Count);

                    try
                    {
                        foreach (var change in upstreamChanges.ToConcreteType())
                        {
                            // Propagate changes as follows:
                            //      Don't propagate moves at all, since we don't preserve indexes.
                            //      For Updates, propagate...
                            //          ...an Add if the new item matches the new type, but the old one doesn't
                            //          ...a Remove if the old item matches the new type, but the new one doesn't
                            //          ...an Update if both items match the new type
                            //          ...nothing if neither items match the new type
                            //      For all other changes, propagate only if the value matches the new type
                            if (change.Reason is ChangeReason.Moved)
                                continue;

                            Change<TDestination, TKey>? downstreamChange = change.Reason switch
                            {
                                ChangeReason.Update => (TypeCheck(change.Previous.Value), TypeCheck(change.Current)) switch
                                {
                                    (true, true) => new(change.Reason, change.Key, Convert(change.Current), change.Previous.Convert(Convert)),
                                    (false, true) => new(ChangeReason.Add, change.Key, Convert(change.Current)),
                                    (true, false) => new(ChangeReason.Remove, change.Key, Convert(change.Previous.Value)),
                                    _ => null,
                                },
                                _ => TypeCheck(change.Current) ? new(change.Reason, change.Key, Convert(change.Current), change.Previous.Convert(Convert)) : null
                            };

                            if (downstreamChange is Change<TDestination, TKey> dsc)
                            {
                                // Do not propagate indexes, we can't guarantee them to be correct, because we aren't caching items.
                                downstreamChanges.Add(dsc);
                            }
                        }

                        if (!suppressEmptyChangeSets || downstreamChanges.Count != 0)
                        {
                            observer.OnNext(downstreamChanges);
                        }
                    }
                    catch (Exception error)
                    {
                        observer.OnError(error);
                    }
                },
                onError: observer.OnError,
                onCompleted: observer.OnCompleted));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TypeCheck(TObject item) => item is TDestination;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static TDestination Convert(TObject item) => item switch
    {
        TDestination destination => destination,
        _ => throw new InvalidCastException($"Value cannot be cast to {typeof(TDestination).Name}"),
    };
}
