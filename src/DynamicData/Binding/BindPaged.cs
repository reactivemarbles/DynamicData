// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace DynamicData.Binding;

/*
 * Binding for the result of the SortAndPage operator
 *
 * (Direct lift from BindVirtualized).
 */
internal sealed class BindPaged<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TObject, TKey>(
    IObservable<IChangeSet<TObject, TKey, PageContext<TObject>>> source,
    IList<TObject> targetList,
    SortAndBindOptions? options)
    where TObject : notnull
    where TKey : notnull
{
    public IObservable<IChangeSet<TObject, TKey>> Run() => options is null
        ? UseContextSortOptions()
        : UseProvidedOptions(options.Value);

    private IObservable<IChangeSet<TObject, TKey>> UseProvidedOptions(SortAndBindOptions sortAndBindOptions) =>
        source.Publish(changes =>
        {
            var comparedChanged = changes
                .Select(changesWithContext => changesWithContext.Context.Comparer)
                .DistinctUntilChanged();

            return changes.SortAndBind(targetList, comparedChanged, sortAndBindOptions);
        });

    private IObservable<IChangeSet<TObject, TKey>> UseContextSortOptions() =>
        Observable.Create<IChangeSet<TObject, TKey>>(observer =>
        {
            var shared = source.Publish();

            var subscriber = new SingleAssignmentDisposable();

            // I tried to make this work without subjects but had issues
            // making the comparedChanged observable to fire. Probably a deadlock
            var changesSubject = new Subject<IChangeSet<TObject, TKey>>();
            var comparerSubject = new ReplaySubject<IComparer<TObject>>(1);
            var bound = false;

            // Until the first element has supplied the binding options there is nothing standing between the
            // source and the observer, so terminal events have to reach the observer directly.
            void Fail(Exception error)
            {
                if (bound)
                {
                    changesSubject.OnError(error);
                }
                else
                {
                    observer.OnError(error);
                }
            }

            void Finish()
            {
                if (bound)
                {
                    changesSubject.OnCompleted();
                }
                else
                {
                    observer.OnCompleted();
                }
            }

            // once we have the initial values, publish as normal.
            var subsequent = shared
                .Skip(1)
                .Subscribe(
                    changesWithContext =>
                    {
                        comparerSubject.OnNext(changesWithContext.Context.Comparer);
                        changesSubject.OnNext(changesWithContext);
                    },
                    Fail,
                    Finish);

            // extract binding options from the page context
            var initial = shared
                .Take(1)
                .Subscribe(changesWithContext =>
                {
                    var virtualOptions = changesWithContext.Context.Options;
                    var extractedOptions = DynamicDataOptions.SortAndBind with
                    {
                        UseBinarySearch = virtualOptions.UseBinarySearch,
                        ResetThreshold = virtualOptions.ResetThreshold
                    };

                    subscriber.Disposable = changesSubject
                            .SortAndBind(targetList, comparerSubject.DistinctUntilChanged(), extractedOptions)
                            .SubscribeSafe(observer);

                    bound = true;

                    comparerSubject.OnNext(changesWithContext.Context.Comparer);
                    changesSubject.OnNext(changesWithContext);
                },
                static _ => { });

            return new CompositeDisposable(initial, subscriber, subsequent, shared.Connect());
        });
}
