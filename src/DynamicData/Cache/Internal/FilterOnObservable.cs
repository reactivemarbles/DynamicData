// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Cache.Internal;
#else

namespace DynamicData.Cache.Internal;
#endif

/// <summary>
/// Provides members for the FilterOnObservable class.
/// </summary>
/// <typeparam name="TObject">The type of the TObject value.</typeparam>
/// <typeparam name="TKey">The type of the TKey value.</typeparam>
/// <param name="source">The source value.</param>
/// <param name="filterFactory">The filterFactory value.</param>
/// <param name="buffer">The buffer value.</param>
/// <param name="scheduler">The scheduler value.</param>
internal sealed class FilterOnObservable<TObject, TKey>(IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IObservable<bool>> filterFactory, TimeSpan? buffer = null, IScheduler? scheduler = null)
    where TObject : notnull
    where TKey : notnull
{
    /// <summary>
    /// The _filterFactory field.
    /// </summary>
    private readonly Func<TObject, TKey, IObservable<bool>> _filterFactory = filterFactory ?? throw new ArgumentNullException(nameof(filterFactory));

    /// <summary>
    /// The _source field.
    /// </summary>
    private readonly IObservable<IChangeSet<TObject, TKey>> _source = source ?? throw new ArgumentNullException(nameof(source));

    /// <summary>
    /// Executes the Run operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public IObservable<IChangeSet<TObject, TKey>> Run() =>
        _source
            .Transform((val, key) => new FilterProxy(val, _filterFactory(val, key)))
            .AutoRefreshOnObservable(proxy => proxy.FilterObservable, buffer, scheduler)
            .Filter(proxy => proxy.PassesFilter)
            .TransformImmutable(proxy => proxy.Value);

/// <summary>
/// Provides members for the FilterProxy class.
/// </summary>
/// <param name="obj">The obj value.</param>
/// <param name="observable">The observable value.</param>
private sealed class FilterProxy(TObject obj, IObservable<bool> observable)
    {
        /// <summary>
        /// Gets the Value value.
        /// </summary>
        public TObject Value { get; } = obj;

        /// <summary>
        /// Gets or sets the PassesFilter value.
        /// </summary>
        public bool PassesFilter { get; private set; }

        /// <summary>
        /// Gets the FilterObservable value.
        /// </summary>
        public IObservable<bool> FilterObservable => observable.DistinctUntilChanged().Do(filterValue => PassesFilter = filterValue);
    }
}
