// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Binding;
#else

namespace DynamicData.Binding;
#endif
/*
 * Binding for the result of the SortAndPage operator
 *
 * (Direct lift from BindVirtualized).
 */

/// <summary>
/// Provides members for the BindPaged class.
/// </summary>
/// <typeparam name="TObject">The type of the TObject value.</typeparam>
/// <typeparam name="TKey">The type of the TKey value.</typeparam>
/// <param name="source">The source value.</param>
/// <param name="targetList">The targetList value.</param>
/// <param name="options">The options value.</param>
internal sealed class BindPaged<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TObject, TKey>(
    IObservable<IChangeSet<TObject, TKey, PageContext<TObject>>> source,
    IList<TObject> targetList,
    SortAndBindOptions? options)
    where TObject : notnull
    where TKey : notnull
{
    /// <summary>
    /// Executes the Run operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public IObservable<IChangeSet<TObject, TKey>> Run() => options is null
        ? UseContextSortOptions()
        : UseProvidedOptions(options.Value);

    /// <summary>
    /// Executes the UseProvidedOptions operation.
    /// </summary>
    /// <param name="sortAndBindOptions">The sortAndBindOptions value.</param>
    /// <returns>The result of the operation.</returns>
    private IObservable<IChangeSet<TObject, TKey>> UseProvidedOptions(SortAndBindOptions sortAndBindOptions) =>
        source.Publish(changes =>
        {
            var comparedChanged = changes
                .Select(changesWithContext => changesWithContext.Context.Comparer)
                .DistinctUntilChanged();

            return changes.SortAndBind(targetList, comparedChanged, sortAndBindOptions);
        });

    /// <summary>
    /// Executes the UseContextSortOptions operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    private IObservable<IChangeSet<TObject, TKey>> UseContextSortOptions() =>
        Observable.Create<IChangeSet<TObject, TKey>>(observer =>
        {
            var shared = source.Publish();

            var subscriber = new SingleAssignmentDisposable();

            // I tried to make this work without subjects but had issues
            // making the comparedChanged observable to fire. Probably a deadlock
            var changesSubject = new Signal<IChangeSet<TObject, TKey>>();
            var comparerSubject = new ReplaySignal<IComparer<TObject>>(1);

            // once we have the initial values, publish as normal.
            var subsequent = shared
                .Skip(1)
                .Subscribe(changesWithContext =>
                {
                    comparerSubject.OnNext(changesWithContext.Context.Comparer);
                    changesSubject.OnNext(changesWithContext);
                });

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

                    comparerSubject.OnNext(changesWithContext.Context.Comparer);
                    changesSubject.OnNext(changesWithContext);
                });

            return new CompositeDisposable(initial, subscriber, subsequent, shared.Connect());
        });
}
