// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

using DynamicData.Reactive.Cache.Internal;
using DynamicData.Reactive.Internal;
#else

using DynamicData.Cache.Internal;
using DynamicData.Internal;
#endif
#if REACTIVE_SHIM

namespace DynamicData.Reactive.List.Internal;
#else

namespace DynamicData.List.Internal;
#endif

/// <summary>
/// Operator that is similiar to MergeMany but intelligently handles Cache ChangeSets.
/// </summary>
/// <typeparam name="TObject">The type of the TObject value.</typeparam>
/// <typeparam name="TDestination">The type of the TDestination value.</typeparam>
/// <typeparam name="TDestinationKey">The type of the TDestinationKey value.</typeparam>
/// <param name="source">The source value.</param>
/// <param name="changeSetSelector">The changeSetSelector value.</param>
/// <param name="equalityComparer">The equalityComparer value.</param>
/// <param name="comparer">The comparer value.</param>
internal sealed class MergeManyCacheChangeSets<TObject, TDestination, TDestinationKey>(IObservable<IChangeSet<TObject>> source, Func<TObject, IObservable<IChangeSet<TDestination, TDestinationKey>>> changeSetSelector, IEqualityComparer<TDestination>? equalityComparer, IComparer<TDestination>? comparer)
    where TObject : notnull
    where TDestination : notnull
    where TDestinationKey : notnull
{
    /// <summary>
    /// Executes the Run operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public IObservable<IChangeSet<TDestination, TDestinationKey>> Run() => Observable.Create<IChangeSet<TDestination, TDestinationKey>>(
        observer => new Subscription(source, changeSetSelector, observer, equalityComparer, comparer));

    /// <summary>
    /// Maintains state for a single subscription.
    /// </summary>
    private sealed class Subscription : CacheParentSubscription<ChangeSetCache<TDestination, TDestinationKey>, int, IChangeSet<TDestination, TDestinationKey>, IChangeSet<TDestination, TDestinationKey>>
    {
        /// <summary>
        /// The _cache field.
        /// </summary>
        private readonly Cache<ChangeSetCache<TDestination, TDestinationKey>, int> _cache = new();

        /// <summary>
        /// The _changeSetMergeTracker field.
        /// </summary>
        private readonly ChangeSetMergeTracker<TDestination, TDestinationKey> _changeSetMergeTracker;

        /// <summary>
        /// The _changeSetSelector field.
        /// </summary>
        private readonly Func<TObject, IObservable<IChangeSet<TDestination, TDestinationKey>>> _changeSetSelector;

        /// <summary>
        /// The _parents field.
        /// </summary>
        private readonly List<ParentItem> _parents = [];

        /// <summary>
        /// The _nextParentKey field.
        /// </summary>
        private int _nextParentKey;

        /// <summary>
        /// Initializes a new instance of the <see cref="Subscription"/> class.
        /// </summary>
        /// <param name="source">The source value.</param>
        /// <param name="changeSetSelector">The changeSetSelector value.</param>
        /// <param name="observer">The observer value.</param>
        /// <param name="equalityComparer">The equalityComparer value.</param>
        /// <param name="comparer">The comparer value.</param>
        public Subscription(
            IObservable<IChangeSet<TObject>> source,
            Func<TObject, IObservable<IChangeSet<TDestination, TDestinationKey>>> changeSetSelector,
            IObserver<IChangeSet<TDestination, TDestinationKey>> observer,
            IEqualityComparer<TDestination>? equalityComparer,
            IComparer<TDestination>? comparer)
            : base(observer)
        {
            _changeSetSelector = changeSetSelector;
            _changeSetMergeTracker = new(() => _cache.Items, comparer, equalityComparer);

            CreateParentSubscription(source.Select(ConvertParentChanges).Where(static changes => changes.Count != 0));
        }

        /// <summary>
        /// Executes the ParentOnNext operation.
        /// </summary>
        /// <param name="changes">The changes value.</param>
        protected override void ParentOnNext(IChangeSet<ChangeSetCache<TDestination, TDestinationKey>, int> changes)
        {
            foreach (var change in changes.ToConcreteType())
            {
                switch (change.Reason)
                {
                    case ChangeReason.Add or ChangeReason.Update:
                        _cache.AddOrUpdate(change.Current, change.Key);
                        AddChildSubscription(change.Current.Source, change.Key);
                        if (change.Previous.HasValue)
                        {
                            _changeSetMergeTracker.RemoveItems(change.Previous.Value.Cache.KeyValues);
                        }

                        break;

                    case ChangeReason.Remove:
                        _cache.Remove(change.Key);
                        _changeSetMergeTracker.RemoveItems(change.Current.Cache.KeyValues);
                        RemoveChildSubscription(change.Key);
                        break;
                }
            }
        }

        /// <summary>
        /// Executes the ChildOnNext operation.
        /// </summary>
        /// <param name="changes">The changes value.</param>
        /// <param name="parentKey">The parentKey value.</param>
        protected override void ChildOnNext(IChangeSet<TDestination, TDestinationKey> changes, int parentKey) =>
            _changeSetMergeTracker.ProcessChangeSet(changes, null);

        /// <summary>
        /// Executes the EmitChanges operation.
        /// </summary>
        /// <param name="observer">The observer value.</param>
        protected override void EmitChanges(IObserver<IChangeSet<TDestination, TDestinationKey>> observer) =>
            _changeSetMergeTracker.EmitChanges(observer);

        /// <summary>
        /// Executes the ConvertParentChanges operation.
        /// </summary>
        /// <param name="changes">The changes value.</param>
        /// <returns>The result of the operation.</returns>
        private IChangeSet<ChangeSetCache<TDestination, TDestinationKey>, int> ConvertParentChanges(IChangeSet<TObject> changes)
        {
            var results = new ChangeSet<ChangeSetCache<TDestination, TDestinationKey>, int>(changes.Count);

            foreach (var change in changes)
            {
                switch (change.Reason)
                {
                    case ListChangeReason.Add:
                        AddParent(change.Item.Current, change.Item.CurrentIndex, results);
                        break;

                    case ListChangeReason.AddRange:
                        AddParents(change.Range, results);
                        break;

                    case ListChangeReason.Remove:
                        RemoveParent(change.Item.Current, change.Item.CurrentIndex, results);
                        break;

                    case ListChangeReason.RemoveRange:
                        RemoveParents(change.Range, results);
                        break;

                    case ListChangeReason.Replace:
                        ReplaceParent(change.Item.Previous.Value, change.Item.Current, change.Item.PreviousIndex, change.Item.CurrentIndex, results);
                        break;

                    case ListChangeReason.Clear:
                        RemoveAllParents(results);
                        break;

                    case ListChangeReason.Moved:
                        MoveParent(change.Item.Current, change.Item.CurrentIndex, change.Item.PreviousIndex);
                        break;
                }
            }

            return results;
        }

        /// <summary>
        /// Executes the AddParents operation.
        /// </summary>
        /// <param name="range">The range value.</param>
        /// <param name="changes">The changes value.</param>
        private void AddParents(RangeChange<TObject> range, ChangeSet<ChangeSetCache<TDestination, TDestinationKey>, int> changes)
        {
            var index = range.Index;
            foreach (var item in range)
            {
                AddParent(item, index, changes);
                if (index >= 0)
                {
                    index++;
                }
            }
        }

        /// <summary>
        /// Executes the AddParent operation.
        /// </summary>
        /// <param name="item">The item value.</param>
        /// <param name="index">The index value.</param>
        /// <param name="changes">The changes value.</param>
        private void AddParent(TObject item, int index, ChangeSet<ChangeSetCache<TDestination, TDestinationKey>, int> changes)
        {
            var key = ++_nextParentKey;
            var child = CreateChild(item);
            var parent = new ParentItem(item, key, child);
            var insertIndex = NormalizeAddIndex(index);

            _parents.Insert(insertIndex, parent);
            changes.Add(new Change<ChangeSetCache<TDestination, TDestinationKey>, int>(ChangeReason.Add, key, child, insertIndex));
        }

        /// <summary>
        /// Executes the ReplaceParent operation.
        /// </summary>
        /// <param name="previous">The previous value.</param>
        /// <param name="current">The current value.</param>
        /// <param name="previousIndex">The previousIndex value.</param>
        /// <param name="currentIndex">The currentIndex value.</param>
        /// <param name="changes">The changes value.</param>
        private void ReplaceParent(TObject previous, TObject current, int previousIndex, int currentIndex, ChangeSet<ChangeSetCache<TDestination, TDestinationKey>, int> changes)
        {
            var replaceIndex = NormalizeExistingIndex(previousIndex);
            if (replaceIndex < 0 || !ReferenceEquals(_parents[replaceIndex].Source, previous))
            {
                replaceIndex = FindParentIndex(previous);
            }

            if (replaceIndex < 0)
            {
                AddParent(current, currentIndex, changes);
                return;
            }

            var existing = _parents[replaceIndex];
            var child = CreateChild(current);
            var updated = new ParentItem(current, existing.Key, child);
            _parents[replaceIndex] = updated;

            var destinationIndex = NormalizeReplacementIndex(currentIndex, replaceIndex);
            if (destinationIndex != replaceIndex)
            {
                _parents.RemoveAt(replaceIndex);
                _parents.Insert(destinationIndex, updated);
            }

            changes.Add(new Change<ChangeSetCache<TDestination, TDestinationKey>, int>(ChangeReason.Update, existing.Key, child, existing.Child, destinationIndex, replaceIndex));
        }

        /// <summary>
        /// Executes the RemoveParents operation.
        /// </summary>
        /// <param name="range">The range value.</param>
        /// <param name="changes">The changes value.</param>
        private void RemoveParents(RangeChange<TObject> range, ChangeSet<ChangeSetCache<TDestination, TDestinationKey>, int> changes)
        {
            if (range.Index >= 0)
            {
                for (var i = 0; i < range.Count; i++)
                {
                    RemoveParentAt(range.Index, changes);
                }

                return;
            }

            foreach (var item in range)
            {
                RemoveParent(item, -1, changes);
            }
        }

        /// <summary>
        /// Executes the RemoveParent operation.
        /// </summary>
        /// <param name="item">The item value.</param>
        /// <param name="index">The index value.</param>
        /// <param name="changes">The changes value.</param>
        private void RemoveParent(TObject item, int index, ChangeSet<ChangeSetCache<TDestination, TDestinationKey>, int> changes)
        {
            var removeIndex = NormalizeExistingIndex(index);
            if (removeIndex < 0 || !ReferenceEquals(_parents[removeIndex].Source, item))
            {
                removeIndex = FindParentIndex(item);
            }

            if (removeIndex >= 0)
            {
                RemoveParentAt(removeIndex, changes);
            }
        }

        /// <summary>
        /// Executes the RemoveParentAt operation.
        /// </summary>
        /// <param name="index">The index value.</param>
        /// <param name="changes">The changes value.</param>
        private void RemoveParentAt(int index, ChangeSet<ChangeSetCache<TDestination, TDestinationKey>, int> changes)
        {
            if (index < 0 || index >= _parents.Count)
            {
                return;
            }

            var parent = _parents[index];
            _parents.RemoveAt(index);
            changes.Add(new Change<ChangeSetCache<TDestination, TDestinationKey>, int>(ChangeReason.Remove, parent.Key, parent.Child, index));
        }

        /// <summary>
        /// Executes the RemoveAllParents operation.
        /// </summary>
        /// <param name="changes">The changes value.</param>
        private void RemoveAllParents(ChangeSet<ChangeSetCache<TDestination, TDestinationKey>, int> changes)
        {
            for (var i = _parents.Count - 1; i >= 0; i--)
            {
                RemoveParentAt(i, changes);
            }
        }

        /// <summary>
        /// Executes the MoveParent operation.
        /// </summary>
        /// <param name="item">The item value.</param>
        /// <param name="currentIndex">The currentIndex value.</param>
        /// <param name="previousIndex">The previousIndex value.</param>
        private void MoveParent(TObject item, int currentIndex, int previousIndex)
        {
            var from = NormalizeExistingIndex(previousIndex);
            if (from < 0 || !ReferenceEquals(_parents[from].Source, item))
            {
                from = FindParentIndex(item);
            }

            if (from < 0)
            {
                return;
            }

            var parent = _parents[from];
            _parents.RemoveAt(from);
            _parents.Insert(NormalizeAddIndex(currentIndex), parent);
        }

        /// <summary>
        /// Executes the FindParentIndex operation.
        /// </summary>
        /// <param name="item">The item value.</param>
        /// <returns>The result of the operation.</returns>
        private int FindParentIndex(TObject item)
        {
            var comparer = EqualityComparer<TObject>.Default;
            for (var i = 0; i < _parents.Count; i++)
            {
                if (ReferenceEquals(_parents[i].Source, item) || comparer.Equals(_parents[i].Source, item))
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Executes the NormalizeAddIndex operation.
        /// </summary>
        /// <param name="index">The index value.</param>
        /// <returns>The result of the operation.</returns>
        private int NormalizeAddIndex(int index) => index < 0 || index > _parents.Count ? _parents.Count : index;

        /// <summary>
        /// Executes the NormalizeExistingIndex operation.
        /// </summary>
        /// <param name="index">The index value.</param>
        /// <returns>The result of the operation.</returns>
        private int NormalizeExistingIndex(int index) => index >= 0 && index < _parents.Count ? index : -1;

        /// <summary>
        /// Executes the NormalizeReplacementIndex operation.
        /// </summary>
        /// <param name="index">The index value.</param>
        /// <param name="fallbackIndex">The fallbackIndex value.</param>
        /// <returns>The result of the operation.</returns>
        private int NormalizeReplacementIndex(int index, int fallbackIndex) => index >= 0 && index < _parents.Count ? index : fallbackIndex;

        /// <summary>
        /// Executes the CreateChild operation.
        /// </summary>
        /// <param name="item">The item value.</param>
        /// <returns>The result of the operation.</returns>
        private ChangeSetCache<TDestination, TDestinationKey> CreateChild(TObject item) =>
            new(MakeChildObservable(_changeSetSelector(item)));

        /// <summary>
        /// Stores a source item and its child container.
        /// </summary>
        /// <param name="Source">The Source value.</param>
        /// <param name="Key">The Key value.</param>
        /// <param name="Child">The Child value.</param>
        private sealed record ParentItem(TObject Source, int Key, ChangeSetCache<TDestination, TDestinationKey> Child);
    }
}
