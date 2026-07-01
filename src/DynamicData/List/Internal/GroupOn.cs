// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.List.Internal;
#else

namespace DynamicData.List.Internal;
#endif

/// <summary>
/// Provides members for the GroupOn class.
/// </summary>
/// <typeparam name="TObject">The type of the TObject value.</typeparam>
/// <typeparam name="TGroupKey">The type of the TGroupKey value.</typeparam>
/// <param name="source">The source value.</param>
/// <param name="groupSelector">The groupSelector value.</param>
/// <param name="regrouper">The regrouper value.</param>
internal sealed class GroupOn<TObject, TGroupKey>(IObservable<IChangeSet<TObject>> source, Func<TObject, TGroupKey> groupSelector, IObservable<Unit>? regrouper)
    where TObject : notnull
    where TGroupKey : notnull
{
    /// <summary>
    /// The _groupSelector field.
    /// </summary>
    private readonly Func<TObject, TGroupKey> _groupSelector = groupSelector ?? throw new ArgumentNullException(nameof(groupSelector));

    /// <summary>
    /// The _regrouper field.
    /// </summary>
    private readonly IObservable<Unit>? _regrouper = regrouper;

    /// <summary>
    /// The _source field.
    /// </summary>
    private readonly IObservable<IChangeSet<TObject>> _source = source ?? throw new ArgumentNullException(nameof(source));

    /// <summary>
    /// Executes the Run operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public IObservable<IChangeSet<IGroup<TObject, TGroupKey>>> Run() => Observable.Create<IChangeSet<IGroup<TObject, TGroupKey>>>(
            observer =>
            {
                var groupings = new ChangeAwareList<IGroup<TObject, TGroupKey>>();
                var groupCache = new Dictionary<TGroupKey, Group<TObject, TGroupKey>>();

                // capture the grouping up front which has the benefit that the group key is only selected once
                var itemsWithGroup = _source.Transform<TObject, ItemWithGroupKey>((t, previous) => new ItemWithGroupKey(t, _groupSelector(t), previous.Convert(p => p.Group)), true);

                var locker = InternalEx.NewLock();
                var shared = itemsWithGroup.Synchronize(locker).Publish();

                var grouper = shared.Select(changes => Process(groupings, groupCache, changes));

                var regrouperFunc = _regrouper is null ?
                    Observable.Never<IChangeSet<IGroup<TObject, TGroupKey>>>() :
                    _regrouper.Synchronize(locker).CombineLatest(shared.ToCollection(), (_, collection) => Regroup(groupings, groupCache, collection));

                var publisher = grouper.Merge(regrouperFunc).DisposeMany() // dispose removes as the grouping is disposable
                    .NotEmpty().SubscribeSafe(observer);

                return new CompositeDisposable(publisher, shared.Connect());
            });

    /// <summary>
    /// Executes the GetCache operation.
    /// </summary>
    /// <param name="groupCaches">The groupCaches value.</param>
    /// <param name="key">The key value.</param>
    /// <returns>The result of the operation.</returns>
    private static GroupWithAddIndicator GetCache(IDictionary<TGroupKey, Group<TObject, TGroupKey>> groupCaches, TGroupKey key)
    {
        var cache = groupCaches.Lookup(key);
        if (cache.HasValue)
        {
            return new GroupWithAddIndicator(cache.Value, false);
        }

        var newcache = new Group<TObject, TGroupKey>(key);
        groupCaches[key] = newcache;
        return new GroupWithAddIndicator(newcache, true);
    }

    /// <summary>
    /// Executes the Process operation.
    /// </summary>
    /// <param name="result">The result value.</param>
    /// <param name="groupCollection">The groupCollection value.</param>
    /// <param name="changes">The changes value.</param>
    /// <returns>The result of the operation.</returns>
    private static IChangeSet<IGroup<TObject, TGroupKey>> Process(ChangeAwareList<IGroup<TObject, TGroupKey>> result, IDictionary<TGroupKey, Group<TObject, TGroupKey>> groupCollection, IChangeSet<ItemWithGroupKey> changes)
    {
        foreach (var grouping in changes.Unified().GroupBy(change => change.Current.Group))
        {
            // lookup group and if created, add to result set
            var currentGroup = grouping.Key;
            var lookup = GetCache(groupCollection, currentGroup);
            var groupCache = lookup.Group;

            if (lookup.WasCreated)
            {
                result.Add(groupCache);
            }

            // start a group edit session, so all changes are batched
            groupCache.Edit(
                list =>
                {
                    // iterate through the group's items and process
                    foreach (var change in grouping)
                    {
                        switch (change.Reason)
                        {
                            case ListChangeReason.Add:
                                {
                                    list.Add(change.Current.Item);
                                    break;
                                }

                            case ListChangeReason.Replace:
                                {
                                    var previousItem = change.Previous.Value.Item;
                                    var previousGroup = change.Previous.Value.Group;

                                    // check whether an item changing has resulted in a different group
                                    if (previousGroup.Equals(currentGroup))
                                    {
                                        // find and replace
                                        var index = list.IndexOf(previousItem);
                                        list[index] = change.Current.Item;
                                    }
                                    else
                                    {
                                        // add to new group
                                        list.Add(change.Current.Item);

                                        // remove from old group
                                        groupCollection.Lookup(previousGroup).IfHasValue(
                                            g =>
                                            {
                                                g.Edit(oldList => oldList.Remove(previousItem));
                                                if (g.List.Count != 0)
                                                {
                                                    return;
                                                }

                                                groupCollection.Remove(g.GroupKey);
                                                result.Remove(g);
                                            });
                                    }

                                    break;
                                }

                            case ListChangeReason.Refresh:
                                {
                                    // 1. Check whether item was in the group and should not be now (or vice versa)
                                    var currentItem = change.Current.Item;
                                    var previousGroup = change.Current.PreviousGroup.Value;

                                    // check whether an item changing has resulted in a different group
                                    if (previousGroup.Equals(currentGroup))
                                    {
                                        // Propagate refresh event
                                        var cal = (ChangeAwareList<TObject>)list;
                                        cal.Refresh(currentItem);
                                    }
                                    else
                                    {
                                        // add to new group
                                        list.Add(currentItem);

                                        // remove from old group if empty
                                        groupCollection.Lookup(previousGroup).IfHasValue(
                                            g =>
                                            {
                                                g.Edit(oldList => oldList.Remove(currentItem));
                                                if (g.List.Count != 0)
                                                {
                                                    return;
                                                }

                                                groupCollection.Remove(g.GroupKey);
                                                result.Remove(g);
                                            });
                                    }

                                    break;
                                }

                            case ListChangeReason.Remove:
                                {
                                    list.Remove(change.Current.Item);
                                    break;
                                }

                            case ListChangeReason.Clear:
                                {
                                    list.Clear();
                                    break;
                                }
                        }
                    }
                });

            if (groupCache.List.Count == 0)
            {
                groupCollection.Remove(groupCache.GroupKey);
                result.Remove(groupCache);
            }
        }

        return result.CaptureChanges();
    }

    /// <summary>
    /// Executes the Regroup operation.
    /// </summary>
    /// <param name="result">The result value.</param>
    /// <param name="groupCollection">The groupCollection value.</param>
    /// <param name="currentItems">The currentItems value.</param>
    /// <returns>The result of the operation.</returns>
    private IChangeSet<IGroup<TObject, TGroupKey>> Regroup(ChangeAwareList<IGroup<TObject, TGroupKey>> result, IDictionary<TGroupKey, Group<TObject, TGroupKey>> groupCollection, IReadOnlyCollection<ItemWithGroupKey> currentItems)
    {
        // TODO: We need to update ItemWithValue>
        foreach (var itemWithValue in currentItems)
        {
            var currentGroupKey = itemWithValue.Group;
            var newGroupKey = _groupSelector(itemWithValue.Item);
            if (newGroupKey.Equals(currentGroupKey))
            {
                continue;
            }

            // remove from the old group
            var currentGroupLookup = GetCache(groupCollection, currentGroupKey);
            var currentGroupCache = currentGroupLookup.Group;
            currentGroupCache.Edit(innerList => innerList.Remove(itemWithValue.Item));

            if (currentGroupCache.List.Count == 0)
            {
                groupCollection.Remove(currentGroupKey);
                result.Remove(currentGroupCache);
            }

            // Mark the old item with the new cache group
            itemWithValue.Group = newGroupKey;

            // add to the new group
            var newGroupLookup = GetCache(groupCollection, newGroupKey);
            var newGroupCache = newGroupLookup.Group;
            newGroupCache.Edit(innerList => innerList.Add(itemWithValue.Item));

            if (newGroupLookup.WasCreated)
            {
                result.Add(newGroupCache);
            }
        }

        return result.CaptureChanges();
    }

/// <summary>
/// Represents the GroupWithAddIndicator value.
/// </summary>
private readonly struct GroupWithAddIndicator
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GroupWithAddIndicator"/> struct.
        /// </summary>
        /// <param name="group">The group value.</param>
        /// <param name="wasCreated">The wasCreated value.</param>
        public GroupWithAddIndicator(Group<TObject, TGroupKey> group, bool wasCreated)
            : this()
        {
            Group = group;
            WasCreated = wasCreated;
        }

        /// <summary>
        /// Gets the Group value.
        /// </summary>
        public Group<TObject, TGroupKey> Group { get; }

        /// <summary>
        /// Gets the WasCreated value.
        /// </summary>
        public bool WasCreated { get; }
    }

/// <summary>
/// Provides members for the ItemWithGroupKey class.
/// </summary>
/// <param name="item">The item value.</param>
/// <param name="group">The group value.</param>
/// <param name="previousGroup">The previousGroup value.</param>
private sealed class ItemWithGroupKey(TObject item, TGroupKey group, ReactiveUI.Primitives.Optional<TGroupKey> previousGroup) : IEquatable<ItemWithGroupKey>
    {
        /// <summary>
        /// Gets or sets the Group value.
        /// </summary>
        public TGroupKey Group { get; set; } = group;

        /// <summary>
        /// Gets the Item value.
        /// </summary>
        public TObject Item { get; } = item;

        /// <summary>
        /// Gets the PreviousGroup value.
        /// </summary>
        public ReactiveUI.Primitives.Optional<TGroupKey> PreviousGroup { get; } = previousGroup;

        /// <summary>Returns a value that indicates whether the values of two <c>GroupOn&lt;TObject, TGroupKey&gt;.ItemWithGroupKey</c> objects are equal.</summary>
        /// <param name="left">The first value to compare.</param>
        /// <param name="right">The second value to compare.</param>
        /// <returns>true if the <paramref name="left" /> and <paramref name="right" /> parameters have the same value; otherwise, false.</returns>
        public static bool operator ==(ItemWithGroupKey left, ItemWithGroupKey right) => Equals(left, right);

        /// <summary>Returns a value that indicates whether two <c>GroupOn&lt;TObject, TGroupKey&gt;.ItemWithGroupKey</c> objects have different values.</summary>
        /// <param name="left">The first value to compare.</param>
        /// <param name="right">The second value to compare.</param>
        /// <returns>true if <paramref name="left" /> and <paramref name="right" /> are not equal; otherwise, false.</returns>
        public static bool operator !=(ItemWithGroupKey left, ItemWithGroupKey right) => !Equals(left, right);

        /// <summary>
        /// Executes the Equals operation.
        /// </summary>
        /// <param name="other">The other value.</param>
        /// <returns>The result of the operation.</returns>
        public bool Equals(ItemWithGroupKey? other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return EqualityComparer<TObject>.Default.Equals(Item, other.Item);
        }

        /// <summary>
        /// Executes the Equals operation.
        /// </summary>
        /// <param name="obj">The obj value.</param>
        /// <returns>The result of the operation.</returns>
        public override bool Equals(object? obj)
        {
            if (obj is null)
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            return obj is ItemWithGroupKey value && Equals(value);
        }

        /// <summary>
        /// Executes the GetHashCode operation.
        /// </summary>
        /// <returns>The result of the operation.</returns>
        public override int GetHashCode() => Item is null ? 0 : EqualityComparer<TObject>.Default.GetHashCode(Item);

        /// <summary>
        /// Executes the ToString operation.
        /// </summary>
        /// <returns>The result of the operation.</returns>
        public override string ToString() => $"{Item} ({Group})";
    }
}
