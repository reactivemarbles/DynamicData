// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.List.Internal;
#else

namespace DynamicData.List.Internal;
#endif

/// <summary>
/// Provides members for the FilterOnObservable class.
/// </summary>
/// <typeparam name="TObject">The type of the TObject value.</typeparam>
/// <param name="source">The source value.</param>
/// <param name="filter">The filter value.</param>
/// <param name="buffer">The buffer value.</param>
/// <param name="scheduler">The scheduler value.</param>
internal sealed class FilterOnObservable<TObject>(IObservable<IChangeSet<TObject>> source, Func<TObject, IObservable<bool>> filter, TimeSpan? buffer = null, IScheduler? scheduler = null)
    where TObject : notnull
{
    /// <summary>
    /// The _filter field.
    /// </summary>
    private readonly Func<TObject, IObservable<bool>> _filter = filter ?? throw new ArgumentNullException(nameof(filter));

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

                var allItems = new List<ObjWithFilterValue>();

                var shared = _source.Synchronize(locker).Transform(v => new ObjWithFilterValue(v, true)) // we default to true (include all items)
                    .Clone(allItems) // clone all items so we can look up the index when a change has been made
                    .Publish();

                // monitor each item observable and create change, carry the value of the observable property
                var itemHasChanged = shared.MergeMany(v => _filter(v.Obj).Select(prop => new ObjWithFilterValue(v.Obj, prop)));

                // create a change set, either buffered or one item at the time
                var itemsChanged = buffer is null ?
                    itemHasChanged.Select(t => new[] { t }) :
                    itemHasChanged.Buffer(buffer.Value, scheduler ?? GlobalConfig.DefaultScheduler).Where(list => list.Count > 0);

                var requiresRefresh = itemsChanged.Synchronize(locker).Select(
                    items => // catch all the indices of items which have been refreshed
                        IndexOfMany(allItems, items, v => v.Obj, (t, idx) => new Change<ObjWithFilterValue>(ListChangeReason.Refresh, t, idx))).Select(changes => new ChangeSet<ObjWithFilterValue>(changes));

                // publish refreshes and underlying changes
                var publisher = shared.Merge(requiresRefresh).Filter(v => v.Filter)
                    .Transform(v => v.Obj)
                    .SuppressRefresh() // suppress refreshes from filter, avoids excessive refresh messages for no-op filter updates
                    .NotEmpty()
                    .SubscribeSafe(observer);

                return new CompositeDisposable(publisher, shared.Connect());
            });

    /// <summary>
    /// Executes the IndexOfMany operation.
    /// </summary>
    /// <typeparam name="TObj">The type of the TObj value.</typeparam>
    /// <typeparam name="TObjectProp">The type of the TObjectProp value.</typeparam>
    /// <typeparam name="TResult">The type of the TResult value.</typeparam>
    /// <param name="source">The source value.</param>
    /// <param name="itemsToFind">The itemsToFind value.</param>
    /// <param name="objectPropertyFunc">The objectPropertyFunc value.</param>
    /// <param name="resultSelector">The resultSelector value.</param>
    /// <returns>The result of the operation.</returns>
    private static IEnumerable<TResult> IndexOfMany<TObj, TObjectProp, TResult>(IEnumerable<TObj> source, IEnumerable<TObj> itemsToFind, Func<TObj, TObjectProp> objectPropertyFunc, Func<TObj, int, TResult> resultSelector)
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(itemsToFind);
        ArgumentExceptionHelper.ThrowIfNull(resultSelector);

        var indexed = source.Select((element, index) => new { Element = element, Index = index });
        return itemsToFind.Join(indexed, objectPropertyFunc, right => objectPropertyFunc(right.Element), (left, right) => resultSelector(left, right.Index));
    }

/// <summary>
/// Represents the ObjWithFilterValue value.
/// </summary>
/// <param name="obj">The obj value.</param>
/// <param name="filter">The filter value.</param>
private readonly struct ObjWithFilterValue(TObject obj, bool filter) : IEquatable<ObjWithFilterValue>
    {
        /// <summary>
        /// The Obj field.
        /// </summary>
        public readonly TObject Obj = obj;

        /// <summary>
        /// The Filter field.
        /// </summary>
        public readonly bool Filter = filter;

        /// <summary>
        /// Gets the ObjComparer value.
        /// </summary>
        private static IEqualityComparer<ObjWithFilterValue> ObjComparer { get; } = new ObjEqualityComparer();

        /// <summary>
        /// Executes the Equals operation.
        /// </summary>
        /// <param name="other">The other value.</param>
        /// <returns>The result of the operation.</returns>
        public bool Equals(ObjWithFilterValue other) =>
            ObjComparer.Equals(this, other); // default equality does _not_ include Filter value, as that would cause the Filter operator that is used later to fail

        /// <summary>
        /// Executes the Equals operation.
        /// </summary>
        /// <param name="obj">The obj value.</param>
        /// <returns>The result of the operation.</returns>
        public override bool Equals(object? obj) => obj is ObjWithFilterValue value && Equals(value);

        /// <summary>
        /// Executes the GetHashCode operation.
        /// </summary>
        /// <returns>The result of the operation.</returns>
        public override int GetHashCode() => ObjComparer.GetHashCode(this);

/// <summary>
/// Provides members for the ObjEqualityComparer class.
/// </summary>
private sealed class ObjEqualityComparer : IEqualityComparer<ObjWithFilterValue>
        {
            /// <summary>
            /// Executes the Equals operation.
            /// </summary>
            /// <param name="x">The x value.</param>
            /// <param name="y">The y value.</param>
            /// <returns>The result of the operation.</returns>
            public bool Equals(ObjWithFilterValue x, ObjWithFilterValue y) => EqualityComparer<TObject>.Default.Equals(x.Obj, y.Obj);

            /// <summary>
            /// Executes the GetHashCode operation.
            /// </summary>
            /// <param name="obj">The obj value.</param>
            /// <returns>The result of the operation.</returns>
            public int GetHashCode(ObjWithFilterValue obj)
            {
                unchecked
                {
                    return (obj.Obj is null ? 0 : EqualityComparer<TObject>.Default.GetHashCode(obj.Obj)) * 397;
                }
            }
        }
    }
}
