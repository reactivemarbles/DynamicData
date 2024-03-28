// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace DynamicData.Binding;

/*
 * Binding for the result of the SortAndVirtualize operator
 */
internal sealed class SortAndBindVirtualized<TObject, TKey>(
    IObservable<IChangeSet<TObject, TKey, VirtualContext<TObject>>> source,
    IList<TObject> targetList,
    SortAndBindOptions? options)
    where TObject : notnull
    where TKey : notnull
{
    public IObservable<IChangeSet<TObject, TKey>> Run() => options is null
        ? UseVirtualSortOptions()
        : UseProvidedOptions(options.Value);

    private IObservable<IChangeSet<TObject, TKey>> UseProvidedOptions(SortAndBindOptions sortAndBindOptions) =>
        source.Publish(changes =>
        {
            var comparedChanged = changes
                .Select(changesWithContext => changesWithContext.Context.Comparer)
                .DistinctUntilChanged();

            return changes.SortAndBind(targetList, comparedChanged, sortAndBindOptions);
        });

    private IObservable<IChangeSet<TObject, TKey>> UseVirtualSortOptions() =>
        Observable.Create<IChangeSet<TObject, TKey>>(observer =>
        {
            var shared = source.Publish();

            // the comparer may or may not change
            var comparedChanged = shared
                .Select(changesWithContext => changesWithContext.Context.Comparer)
                .DistinctUntilChanged();

            var subscriber = new SingleAssignmentDisposable();
            var subject = new Subject<IChangeSet<TObject, TKey>>();

            // once we have the initial values, publish as normal.
            var subsequent = shared
                .Skip(1)
                .Subscribe(changesWithContext =>
                {
                    subject.OnNext(changesWithContext);
                });

            // extract binding options from the virtual context
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

                    subscriber.Disposable = subject
                            .SortAndBind(targetList, comparedChanged, extractedOptions)
                            .SubscribeSafe(observer);

                    subject.OnNext(changesWithContext);
                });
            return new CompositeDisposable(initial, subscriber, subsequent, shared.Connect());
        });
}
