using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using DynamicData.Annotations;
using DynamicData.Kernel;

namespace DynamicData.List.Internal
{
    internal class Filter<T>
    {
        private readonly IObservable<IChangeSet<T>> _source;
        private readonly IObservable<Func<T, bool>> _predicates;

        public Filter([NotNull] IObservable<IChangeSet<T>> source,
                             [NotNull] IObservable<Func<T, bool>> predicates)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _predicates = predicates ?? throw new ArgumentNullException(nameof(predicates));
        }

        public IObservable<IChangeSet<T>> Run()
        {
            return Observable.Create<IChangeSet<T>>(observer =>
            {
                var locker = new object();
                
                Func<T, bool> predicate = t => false;
                var all = new List<ItemWithMatch>();
                var filtered = new ChangeAwareList<ItemWithMatch>();
                
                //requery when predicate changes
                var predicateChanged = _predicates
                    .Synchronize(locker)
                    .Select(newPredicate =>
                    {
                        predicate = newPredicate;
                        Requery(predicate, all, filtered);
                        return filtered.CaptureChanges();
                    });

                /*
                 * Apply the transform operator so 'IsMatch' state can be evalutated and captured one time only
                 * This is to eliminate the need to re-apply the predicate when determining whether an item was previously matched,
                 * which is essential when we have mutable state
                 */

                //Need to get item by index and store it in the transform
                var filteredResult = _source
                    .Synchronize(locker)
                    .Transform<T, ItemWithMatch>((t, previous, idx) =>
                    {
                        var wasMatch = previous.ConvertOr(p => p.IsMatch, () => false);
                        return new ItemWithMatch(t, idx, predicate(t), wasMatch);
                    },true)
                    .Select(changes =>
                    {
                        all.Clone(changes); //keep track of all changes
                        Process( filtered, changes);
                        return filtered.CaptureChanges();
                    });
                
                return predicateChanged.Merge(filteredResult)
                            .NotEmpty()
                            .Select(changes => changes.Transform(iwm => iwm.Item)) // use convert, not transform
                            .SubscribeSafe(observer);
            });
        }

        private void Process( ChangeAwareList<ItemWithMatch> filtered, IChangeSet<ItemWithMatch> changes)
        {
            //Maintain all items as well as filtered list. This enables us to a) requery when the predicate changes b) check the previous state when Refresh is called
            foreach (var item in changes)
            {
                switch (item.Reason)
                {
                    case ListChangeReason.Add:
                    {
                        var change = item.Item;
                        if (change.Current.IsMatch)
                            filtered.Add(change.Current);
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
                                //an update, so get the latest index and pass the index up the chain
                                var previous = filtered.Select(x => x.Item)
                                    .IndexOfOptional(change.Previous.Value.Item, ReferenceEqualityComparer<T>.Instance)
                                    .ValueOrThrow(() => new InvalidOperationException($"Cannot find index of {typeof(T).Name} -> {change.Previous.Value}. Expected to be in the list"));

                                    //replace inline
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
                                //an update, so get the latest index and pass the index up the chain
                                var previous = filtered.Select(x => x.Item)
                                    .IndexOfOptional(change.Current.Item, ReferenceEqualityComparer<T>.Instance)
                                    .ValueOrThrow(() => new InvalidOperationException($"Cannot find index of {typeof(T).Name} -> {change.Previous.Value}. Expected to be in the list"));

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
                                filtered.Remove(change.Previous.Value);
                        }
                        break;
                    }
                    case ListChangeReason.Remove:
                    {
                        var change = item.Item;
                        if (change.Current.IsMatch)
                            filtered.Remove(change.Current);
                        break;
                    }
                    case ListChangeReason.RemoveRange:
                    {
                        filtered.RemoveMany(item.Range.Where(t => t.IsMatch));
                        break;
                    }
                    case ListChangeReason.Clear:
                    {
                        filtered.ClearOrRemoveMany(item);
                        break;
                    }
                }
            }
        }

        private void Requery(Func<T, bool> predicate, List<ItemWithMatch> all, ChangeAwareList<ItemWithMatch> filtered)
        {
            var mutatedMatches = new List<Action>(all.Count);

            var newState = all.Select(item =>
            {
                var match = predicate(item.Item);
                var wasMatch = item.IsMatch;

                //Mutate match - defer until filtered list has been modified
                //[to prevent potential IndexOf failures]
                if (item.IsMatch != match)
                    mutatedMatches.Add(()=> item.IsMatch = match);

                return new
                {
                    Item = item,
                    IsMatch = match,
                    WasMatch = wasMatch
                };
            }).ToList();

            //reflect items which are no longer matched
            var noLongerMatched = newState.Where(state => !state.IsMatch && state.WasMatch).Select(state => state.Item);
            filtered.RemoveMany(noLongerMatched);

            //reflect new matches in the list
            var newMatched = newState.Where(state => state.IsMatch && !state.WasMatch).Select(state => state.Item);
            filtered.AddRange(newMatched);

            //finally apply mutations
            mutatedMatches.ForEach(m => m());
        }

        private sealed class ItemWithMatch : IEquatable<ItemWithMatch>
        {
            public T Item { get; }
            public int Index { get; }
            public bool IsMatch { get; set; }
            public bool WasMatch { get; set; }

            public ItemWithMatch(T item,int index, bool isMatch, bool wasMatch = false)
            {
                Item = item;
                Index = index;
                IsMatch = isMatch;
                WasMatch = wasMatch;
            }

            #region Equality

            public bool Equals(ItemWithMatch other)
            {
                if (ReferenceEquals(null, other)) return false;
                if (ReferenceEquals(this, other)) return true;
                return EqualityComparer<T>.Default.Equals(Item, other.Item);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((ItemWithMatch) obj);
            }

            public override int GetHashCode()
            {
                return EqualityComparer<T>.Default.GetHashCode(Item);
            }

            /// <summary>Returns a value that indicates whether the values of two <see cref="T:DynamicData.List.Internal.Filter`1.ItemWithMatch" /> objects are equal.</summary>
            /// <param name="left">The first value to compare.</param>
            /// <param name="right">The second value to compare.</param>
            /// <returns>true if the <paramref name="left" /> and <paramref name="right" /> parameters have the same value; otherwise, false.</returns>
            public static bool operator ==(ItemWithMatch left, ItemWithMatch right)
            {
                return Equals(left, right);
            }

            /// <summary>Returns a value that indicates whether two <see cref="T:DynamicData.List.Internal.Filter`1.ItemWithMatch" /> objects have different values.</summary>
            /// <param name="left">The first value to compare.</param>
            /// <param name="right">The second value to compare.</param>
            /// <returns>true if <paramref name="left" /> and <paramref name="right" /> are not equal; otherwise, false.</returns>
            public static bool operator !=(ItemWithMatch left, ItemWithMatch right)
            {
                return !Equals(left, right);
            }

            #endregion

            public override string ToString()
            {
                return $"{Item} @ {Index}, (was {IsMatch} is {WasMatch}";
            }
        }
    }
}
