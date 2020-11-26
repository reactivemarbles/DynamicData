// Copyright (c) 2011-2020 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;

using DynamicData.Kernel;

namespace DynamicData.List.Internal
{
    internal class Filter<T>
    {
        private readonly ListFilterPolicy _policy;

        private readonly Func<T, bool>? _predicate;

        private readonly IObservable<Func<T, bool>>? _predicates;

        private readonly IObservable<IChangeSet<T>> _source;

        public Filter(IObservable<IChangeSet<T>> source, IObservable<Func<T, bool>> predicates, ListFilterPolicy policy = ListFilterPolicy.CalculateDiff)
        {
            _policy = policy;
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _predicates = predicates ?? throw new ArgumentNullException(nameof(predicates));
        }

        public Filter(IObservable<IChangeSet<T>> source, Func<T, bool> predicate, ListFilterPolicy policy = ListFilterPolicy.CalculateDiff)
        {
            _policy = policy;
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
        }

        public IObservable<IChangeSet<T>> Run()
        {
            return Observable.Create<IChangeSet<T>>(
                observer =>
                    {
                        var locker = new object();

                        Func<T, bool> predicate = t => false;
                        var all = new List<ItemWithMatch>();
                        var filtered = new ChangeAwareList<ItemWithMatch>();
                        var immutableFilter = _predicate is not null;

                        IObservable<IChangeSet<ItemWithMatch>> predicateChanged;

                        if (immutableFilter)
                        {
                            predicateChanged = Observable.Never<IChangeSet<ItemWithMatch>>();
                            predicate = _predicate ?? predicate;
                        }
                        else
                        {
                            if (_predicates is null)
                            {
                                throw new InvalidOperationException("The predicates is not set and the change is not a immutableFilter.");
                            }

                            predicateChanged = _predicates.Synchronize(locker).Select(
                                newPredicate =>
                                    {
                                        predicate = newPredicate;
                                        return Requery(predicate, all, filtered);
                                    });
                        }

                        /*
                         * Apply the transform operator so 'IsMatch' state can be evaluated and captured one time only
                         * This is to eliminate the need to re-apply the predicate when determining whether an item was previously matched,
                         * which is essential when we have mutable state
                         */

                        // Need to get item by index and store it in the transform
                        var filteredResult = _source.Synchronize(locker).Transform<T, ItemWithMatch>(
                            (t, previous) =>
                                {
                                    var wasMatch = previous.ConvertOr(p => p.IsMatch, () => false);
                                    return new ItemWithMatch(t, predicate(t), wasMatch);
                                },
                            true).Select(
                            changes =>
                                {
                                    // keep track of all changes if filtering on an observable
                                    if (!immutableFilter)
                                    {
                                        all.Clone(changes);
                                    }

                                    return Process(filtered, changes);
                                });

                        return predicateChanged.Merge(filteredResult).NotEmpty().Select(changes => changes.Transform(iwm => iwm.Item)) // use convert, not transform
                            .SubscribeSafe(observer);
                    });
        }

        private static IChangeSet<ItemWithMatch> Process(ChangeAwareList<ItemWithMatch> filtered, IChangeSet<ItemWithMatch> changes)
        {
            // Maintain all items as well as filtered list. This enables us to a) requery when the predicate changes b) check the previous state when Refresh is called
            foreach (var item in changes)
            {
                switch (item.Reason)
                {
                    case ListChangeReason.Add:
                        {
                            var change = item.Item;
                            if (change.Current.IsMatch)
                            {
                                filtered.Add(change.Current);
                            }

                            break;
                        }

                    case ListChangeReason.AddRange:
                        {
                            var matches = item.Range.Where(t => t.IsMatch).ToList();
                            filtered.AddRange(matches);
                            break;
                        }

                    case ListChangeReason.Replace:
                        {
                            var change = item.Item;
                            var match = change.Current.IsMatch;
                            var wasMatch = item.Item.Current.WasMatch;
                            if (match)
                            {
                                if (wasMatch)
                                {
                                    // an update, so get the latest index and pass the index up the chain
                                    var previous = filtered.Select(x => x.Item).IndexOfOptional(change.Previous.Value.Item).ValueOrThrow(() => new InvalidOperationException($"Cannot find index of {typeof(T).Name} -> {change.Previous.Value}. Expected to be in the list"));

                                    // replace inline
                                    filtered[previous.Index] = change.Current;
                                }
                                else
                                {
                                    filtered.Add(change.Current);
                                }
                            }
                            else
                            {
                                if (wasMatch)
                                {
                                    filtered.Remove(change.Previous.Value);
                                }
                            }

                            break;
                        }

                    case ListChangeReason.Refresh:
                        {
                            var change = item.Item;
                            var match = change.Current.IsMatch;
                            var wasMatch = item.Item.Current.WasMatch;
                            if (match)
                            {
                                if (wasMatch)
                                {
                                    // an update, so get the latest index and pass the index up the chain
                                    var previous = filtered.Select(x => x.Item).IndexOfOptional(change.Current.Item).ValueOrThrow(() => new InvalidOperationException($"Cannot find index of {typeof(T).Name} -> {change.Previous.Value}. Expected to be in the list"));

                                    filtered.RefreshAt(previous.Index);
                                }
                                else
                                {
                                    filtered.Add(change.Current);
                                }
                            }
                            else
                            {
                                if (wasMatch)
                                {
                                    filtered.Remove(change.Current);
                                }
                            }

                            break;
                        }

                    case ListChangeReason.Remove:
                        {
                            filtered.Remove(item.Item.Current);
                            break;
                        }

                    case ListChangeReason.RemoveRange:
                        {
                            filtered.RemoveMany(item.Range);
                            break;
                        }

                    case ListChangeReason.Clear:
                        {
                            filtered.ClearOrRemoveMany(item);
                            break;
                        }
                }
            }

            return filtered.CaptureChanges();
        }

        private IChangeSet<ItemWithMatch> Requery(Func<T, bool> predicate, List<ItemWithMatch> all, ChangeAwareList<ItemWithMatch> filtered)
        {
            if (all.Count == 0)
            {
                return ChangeSet<ItemWithMatch>.Empty;
            }

            if (_policy == ListFilterPolicy.ClearAndReplace)
            {
                var itemsWithMatch = all.Select(iwm => new ItemWithMatch(iwm.Item, predicate(iwm.Item), iwm.IsMatch)).ToList();

                // mark items as matched?
                filtered.Clear();
                filtered.AddRange(itemsWithMatch.Where(iwm => iwm.IsMatch));

                // reset state for all items
                all.Clear();
                all.AddRange(itemsWithMatch);
                return filtered.CaptureChanges();
            }

            var toAdd = new List<ItemWithMatch>(all.Count);
            var toRemove = new List<ItemWithMatch>(all.Count);

            for (int i = 0; i < all.Count; i++)
            {
                var original = all[i];

                var newItem = new ItemWithMatch(original.Item, predicate(original.Item), original.IsMatch);
                all[i] = newItem;

                if (newItem.IsMatch && !newItem.WasMatch)
                {
                    toAdd.Add(newItem);
                }
                else if (!newItem.IsMatch && newItem.WasMatch)
                {
                    toRemove.Add(newItem);
                }
            }

            filtered.RemoveMany(toRemove);
            filtered.AddRange(toAdd);

            return filtered.CaptureChanges();
        }

        private readonly struct ItemWithMatch : IEquatable<ItemWithMatch>
        {
            public ItemWithMatch(T item, bool isMatch, bool wasMatch = false)
                : this()
            {
                Item = item;
                IsMatch = isMatch;
                WasMatch = wasMatch;
            }

            public T Item { get; }

            public bool IsMatch { get; }

            public bool WasMatch { get; }

            /// <summary>Returns a value that indicates whether the values of two <see cref="DynamicData.List.Internal.Filter{T}.ItemWithMatch" /> objects are equal.</summary>
            /// <param name="left">The first value to compare.</param>
            /// <param name="right">The second value to compare.</param>
            /// <returns>true if the <paramref name="left" /> and <paramref name="right" /> parameters have the same value; otherwise, false.</returns>
            public static bool operator ==(ItemWithMatch left, ItemWithMatch right)
            {
                return Equals(left, right);
            }

            /// <summary>Returns a value that indicates whether two <see cref="DynamicData.List.Internal.Filter{T}.ItemWithMatch" /> objects have different values.</summary>
            /// <param name="left">The first value to compare.</param>
            /// <param name="right">The second value to compare.</param>
            /// <returns>true if <paramref name="left" /> and <paramref name="right" /> are not equal; otherwise, false.</returns>
            public static bool operator !=(ItemWithMatch left, ItemWithMatch right)
            {
                return !Equals(left, right);
            }

            public bool Equals(ItemWithMatch other)
            {
                return EqualityComparer<T>.Default.Equals(Item, other.Item);
            }

            public override bool Equals(object? obj)
            {
                return obj is ItemWithMatch value && Equals(value);
            }

            public override int GetHashCode()
            {
                return Item is null ? 0 : EqualityComparer<T>.Default.GetHashCode(Item);
            }

            public override string ToString() => $"{Item}, (was {IsMatch} is {WasMatch}";
        }
    }
}