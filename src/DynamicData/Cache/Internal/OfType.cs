// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Linq;
using DynamicData.Internal;
using DynamicData.Kernel;

namespace DynamicData.Cache.Internal;

internal sealed class OfType<TObject, TKey, TDestination>(IObservable<IChangeSet<TObject, TKey>> source, bool suppressEmptyChangeSets)
    where TObject : notnull
    where TKey : notnull
    where TDestination : notnull
{
    public IObservable<IChangeSet<TDestination, TKey>> Run() =>
        Observable.Create<IChangeSet<TDestination, TKey>>(observer => source
            .SubscribeSafe(
                onNext: upstreamChanges =>
                {
                    var downstreamChanges = new ChangeSet<TDestination, TKey>(capacity: upstreamChanges.Count);

                    try
                    {
                        foreach (var change in upstreamChanges.ToConcreteType())
                        {
                            // Don't propagate moves at all, since we don't preserve indexes.
                            if (change.Reason is ChangeReason.Moved)
                                continue;

                            Change<TDestination, TKey>? transformedChange = (change.Reason, change.Current) switch
                            {
                                // Update when Current is the right type, but the Previous was not (Add)
                                (ChangeReason.Update, TDestination addDestination) when change.Previous.Value is not TDestination =>
                                    new(ChangeReason.Add, change.Key, addDestination),

                                // Update when Current is not the right type, but the Previous was (Remove)
                                (ChangeReason.Update, not TDestination) when change.Previous.Value is TDestination removeDestination =>
                                    new(ChangeReason.Remove, change.Key, removeDestination),

                                // For any other change reason, if the Current is the right type, forward with converted types
                                (_, TDestination otherDestination) =>
                                    new(change.Reason, change.Key, otherDestination, change.Previous.HasValue && change.Previous.Value is TDestination pd ? Optional.Some(pd) : default),

                                // Otherwise, don't do anything at all
                                _ => default,
                            };

                            if (transformedChange is { } c)
                            {
                                // Do not propagate indexes, we can't guarantee them to be correct, because we aren't caching items.
                                downstreamChanges.Add(c);
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
}
