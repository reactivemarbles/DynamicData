// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Cache.Internal;
#else

namespace DynamicData.Cache.Internal;
#endif

/// <summary>
/// Provides members for the DisposeMany class.
/// </summary>
/// <typeparam name="TObject">The type of the TObject value.</typeparam>
/// <typeparam name="TKey">The type of the TKey value.</typeparam>
/// <param name="source">The source value.</param>
internal sealed class DisposeMany<TObject, TKey>(IObservable<IChangeSet<TObject, TKey>> source)
    where TObject : notnull
    where TKey : notnull
{
    /// <summary>
    /// The _source field.
    /// </summary>
    private readonly IObservable<IChangeSet<TObject, TKey>> _source = source;

    /// <summary>
    /// Executes the Run operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public IObservable<IChangeSet<TObject, TKey>> Run() =>
        Observable.Create<IChangeSet<TObject, TKey>>(observer =>
        {
            var tracked = new KeyedDisposable<TKey>();

            var sourceSubscription = _source
                .SubscribeSafe(Observer.Create<IChangeSet<TObject, TKey>>(
                    onNext: changeSet =>
                    {
                        observer.OnNext(changeSet);

                        foreach (var change in changeSet.ToConcreteType())
                        {
                            switch (change.Reason)
                            {
                                case ChangeReason.Add or ChangeReason.Update:
                                    tracked.Add(change.Key, change.Current);
                                    break;

                                case ChangeReason.Remove:
                                    tracked.Remove(change.Key);
                                    break;
                            }
                        }
                    },
                    onError: observer.OnError,
                    onCompleted: observer.OnCompleted));

            return new CompositeDisposable(sourceSubscription, tracked);
        });
}
