// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.List.Internal;
#else

namespace DynamicData.List.Internal;
#endif

/// <summary>
/// Provides members for the Filter class.
/// </summary>
internal static partial class Filter
{
/// <summary>
/// Provides members for the Dynamic class.
/// </summary>
/// <typeparam name="T">The type of the T value.</typeparam>
internal sealed class Dynamic<T>
        where T : notnull
    {
        /// <summary>
        /// The _policy field.
        /// </summary>
        private readonly ListFilterPolicy _policy;

        /// <summary>
        /// The _predicate field.
        /// </summary>
        private readonly Func<T, bool>? _predicate;

        /// <summary>
        /// The _predicates field.
        /// </summary>
        private readonly IObservable<Func<T, bool>>? _predicates;

        /// <summary>
        /// The _source field.
        /// </summary>
        private readonly IObservable<IChangeSet<T>> _source;

        /// <summary>
        /// Initializes a new instance of the <see cref="Dynamic{T}"/> class.
        /// </summary>
        /// <param name="source">The source value.</param>
        /// <param name="predicates">The predicates value.</param>
        /// <param name="policy">The policy value.</param>
        public Dynamic(IObservable<IChangeSet<T>> source, IObservable<Func<T, bool>> predicates, ListFilterPolicy policy = ListFilterPolicy.CalculateDiff)
        {
            _policy = policy;
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _predicates = predicates ?? throw new ArgumentNullException(nameof(predicates));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Dynamic{T}"/> class.
        /// </summary>
        /// <param name="source">The source value.</param>
        /// <param name="predicate">The predicate value.</param>
        /// <param name="policy">The policy value.</param>
        public Dynamic(IObservable<IChangeSet<T>> source, Func<T, bool> predicate, ListFilterPolicy policy = ListFilterPolicy.CalculateDiff)
        {
            _policy = policy;
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
        }

        /// <summary>
        /// Executes the Run operation.
        /// </summary>
        /// <returns>The result of the operation.</returns>
        public IObservable<IChangeSet<T>> Run() => Observable.Create<IChangeSet<T>>(
                observer =>
                {
                    var locker = InternalEx.NewLock();

                    Func<T, bool> predicate = _ => false;
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
                                var wasMatch = previous.ConvertOr(p => p!.IsMatch, () => false);
                                return new ItemWithMatch(t, predicate(t), wasMatch);
                            },
                        true)
                        .Select(changes =>
                        {
                            // keep track of all changes if filtering on an observable
                            if (!immutableFilter)
                            {
                                all.Clone(changes);
                            }

                            return Process(filtered, changes);
                        });

                    return predicateChanged.Merge(filteredResult).NotEmpty()
                        .Select(changes => changes.Transform(iwm => iwm.Item)) // use convert, not transform
                        .SubscribeSafe(observer);
                });

        /// <summary>
        /// Executes the Process operation.
        /// </summary>
        /// <param name="filtered">The filtered value.</param>
        /// <param name="changes">The changes value.</param>
        /// <returns>The result of the operation.</returns>
        private static IChangeSet<ItemWithMatch> Process(ChangeAwareList<ItemWithMatch> filtered, IChangeSet<ItemWithMatch> changes)
        {
            // Maintain all items as well as filtered list. This enables us to a) re-query when the predicate changes b) check the previous state when Refresh is called
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
                            else if (wasMatch)
                            {
                                filtered.Remove(change.Previous.Value);
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
                            else if (wasMatch)
                            {
                                filtered.Remove(change.Current);
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

        /// <summary>
        /// Executes the Requery operation.
        /// </summary>
        /// <param name="predicate">The predicate value.</param>
        /// <param name="all">The all value.</param>
        /// <param name="filtered">The filtered value.</param>
        /// <returns>The result of the operation.</returns>
        private IChangeSet<ItemWithMatch> Requery(Func<T, bool> predicate, List<ItemWithMatch> all, ChangeAwareList<ItemWithMatch> filtered)
        {
            if (all.Count == 0)
            {
                return ChangeSet<ItemWithMatch>.Empty;
            }

            if (_policy == ListFilterPolicy.ClearAndReplace)
            {
                var itemsWithMatch = all.ConvertAll(iwm => new ItemWithMatch(iwm.Item, predicate(iwm.Item), iwm.IsMatch));

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

            for (var i = 0; i < all.Count; i++)
            {
                var original = all[i];

                var newItem = new ItemWithMatch(original.Item, predicate(original.Item), original.IsMatch);

                var current = all[i];
                current.IsMatch = newItem.IsMatch;
                current.WasMatch = newItem.WasMatch;

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

/// <summary>
/// Provides members for the ItemWithMatch class.
/// </summary>
/// <param name="item">The item value.</param>
/// <param name="isMatch">The isMatch value.</param>
/// <param name="wasMatch">The wasMatch value.</param>
private sealed class ItemWithMatch(T item, bool isMatch, bool wasMatch = false) : IEquatable<ItemWithMatch>
        {
            /// <summary>
            /// Gets the Item value.
            /// </summary>
            public T Item { get; } = item;

            /// <summary>
            /// Gets or sets the IsMatch value.
            /// </summary>
            public bool IsMatch { get; set; } = isMatch;

            /// <summary>
            /// Gets or sets the WasMatch value.
            /// </summary>
            public bool WasMatch { get; set; } = wasMatch;

            /// <summary>
            /// Executes the operator operation.
            /// </summary>
            /// <param name="left">The left value.</param>
            /// <param name="right">The right value.</param>
            /// <returns>The result of the operation.</returns>
            public static bool operator ==(ItemWithMatch? left, ItemWithMatch? right) =>
                Equals(left, right);

            /// <summary>
            /// Executes the operator operation.
            /// </summary>
            /// <param name="left">The left value.</param>
            /// <param name="right">The right value.</param>
            /// <returns>The result of the operation.</returns>
            public static bool operator !=(ItemWithMatch? left, ItemWithMatch? right) =>
                !Equals(left, right);

            /// <summary>
            /// Executes the Equals operation.
            /// </summary>
            /// <param name="other">The other value.</param>
            /// <returns>The result of the operation.</returns>
            public bool Equals(ItemWithMatch? other)
            {
                if (other is null)
                {
                    return false;
                }

                if (ReferenceEquals(this, other))
                {
                    return true;
                }

                return EqualityComparer<T>.Default.Equals(Item, other.Item);
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

                if (obj.GetType() != GetType())
                {
                    return false;
                }

                return Equals((ItemWithMatch)obj);
            }

            /// <summary>
            /// Executes the GetHashCode operation.
            /// </summary>
            /// <returns>The result of the operation.</returns>
            public override int GetHashCode() => EqualityComparer<T>.Default.GetHashCode(Item!);

            /// <summary>
            /// Executes the ToString operation.
            /// </summary>
            /// <returns>The result of the operation.</returns>
            public override string ToString() => $"{Item}, (was {IsMatch} is {WasMatch}";
        }
    }
}
