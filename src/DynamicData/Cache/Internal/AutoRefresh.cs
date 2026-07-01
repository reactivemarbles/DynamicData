// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Cache.Internal;
#else

namespace DynamicData.Cache.Internal;
#endif

/// <summary>
/// Provides members for the AutoRefresh class.
/// </summary>
/// <typeparam name="TObject">The type of the TObject value.</typeparam>
/// <typeparam name="TKey">The type of the TKey value.</typeparam>
/// <typeparam name="TAny">The type of the TAny value.</typeparam>
/// <param name="source">The source value.</param>
/// <param name="reEvaluator">The reEvaluator value.</param>
/// <param name="buffer">The buffer value.</param>
/// <param name="scheduler">The scheduler value.</param>
internal sealed class AutoRefresh<TObject, TKey, TAny>(IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IObservable<TAny>> reEvaluator, TimeSpan? buffer = null, IScheduler? scheduler = null)
    where TObject : notnull
    where TKey : notnull
{
    /// <summary>
    /// The _reEvaluator field.
    /// </summary>
    private readonly Func<TObject, TKey, IObservable<TAny>> _reEvaluator = reEvaluator ?? throw new ArgumentNullException(nameof(reEvaluator));

    /// <summary>
    /// The _scheduler field.
    /// </summary>
    private readonly IScheduler _scheduler = scheduler ?? GlobalConfig.DefaultScheduler;

    /// <summary>
    /// The _source field.
    /// </summary>
    private readonly IObservable<IChangeSet<TObject, TKey>> _source = source ?? throw new ArgumentNullException(nameof(source));

    /// <summary>
    /// Executes the Run operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public IObservable<IChangeSet<TObject, TKey>> Run() => Observable.Create<IChangeSet<TObject, TKey>>(
            observer =>
            {
                var shared = _source.Publish();

                // monitor each item observable and create change
                var changes = shared.MergeMany((t, k) => _reEvaluator(t, k).Select(_ => new Change<TObject, TKey>(ChangeReason.Refresh, k, t)));

                // create a change set, either buffered or one item at the time
                IObservable<IChangeSet<TObject, TKey>> refreshChanges = buffer is null ?
                    changes.Select(c => new ChangeSet<TObject, TKey>(new[] { c })) :
                    changes.Buffer(buffer.Value, _scheduler).Where(list => list.Count > 0).Select(items => new ChangeSet<TObject, TKey>(items));

                // publish refreshes and underlying changes
                var queue = new SharedDeliveryQueue();
                var publisher = shared.SynchronizeSafe(queue).Merge(refreshChanges.SynchronizeSafe(queue)).SubscribeSafe(observer);

                return new CompositeDisposable(publisher, shared.Connect(), queue);
            });
}
