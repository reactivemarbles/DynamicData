// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.List.Internal;
#else

namespace DynamicData.List.Internal;
#endif

/// <summary>
/// Provides members for the AutoRefresh class.
/// </summary>
/// <typeparam name="TObject">The type of the TObject value.</typeparam>
/// <typeparam name="TAny">The type of the TAny value.</typeparam>
/// <param name="source">The source value.</param>
/// <param name="reEvaluator">The reEvaluator value.</param>
/// <param name="buffer">The buffer value.</param>
/// <param name="scheduler">The scheduler value.</param>
internal sealed class AutoRefresh<TObject, TAny>(IObservable<IChangeSet<TObject>> source, Func<TObject, IObservable<TAny>> reEvaluator, TimeSpan? buffer = null, IScheduler? scheduler = null)
    where TObject : notnull
{
    /// <summary>
    /// The _reEvaluator field.
    /// </summary>
    private readonly Func<TObject, IObservable<TAny>> _reEvaluator = reEvaluator ?? throw new ArgumentNullException(nameof(reEvaluator));

    /// <summary>
    /// The _source field.
    /// </summary>
    private readonly IObservable<IChangeSet<TObject>> _source = source ?? throw new ArgumentNullException(nameof(source));

    /// <summary>
    /// Executes the Run operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public IObservable<IChangeSet<TObject>> Run() => Observable.Create<IChangeSet<TObject>>(
            observer =>
            {
                var locker = InternalEx.NewLock();

                var allItems = new List<TObject>();

                var shared = _source.Synchronize(locker).Clone(allItems) // clone all items so we can look up the index when a change has been made
                    .Publish();

                // monitor each item observable and create change
                var itemHasChanged = shared.MergeMany((t) => _reEvaluator(t).Select(_ => t));

                // create a change set, either buffered or one item at the time
                IObservable<IEnumerable<TObject>> itemsChanged = buffer is null ?
                    itemHasChanged.Select(t => new[] { t }) :
                    itemHasChanged.Buffer(buffer.Value, scheduler ?? GlobalConfig.DefaultScheduler).Where(list => list.Count > 0);

                IObservable<IChangeSet<TObject>> requiresRefresh = itemsChanged.Synchronize(locker).Select(
                    items => // catch all the indices of items which have been refreshed
                        allItems.IndexOfMany(items, (t, idx) => new Change<TObject>(ListChangeReason.Refresh, t, idx))).Select(changes => new ChangeSet<TObject>(changes));

                // publish refreshes and underlying changes
                var publisher = shared.Merge(requiresRefresh).SubscribeSafe(observer);

                return new CompositeDisposable(publisher, shared.Connect());
            });
}
