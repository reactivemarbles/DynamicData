using System;
using System.Collections.Generic;
using System.Linq;
using DynamicData.Annotations;
using DynamicData.Kernel;
using System.Collections.ObjectModel;

// ReSharper disable once CheckNamespace
namespace DynamicData
{
    /// <summary>
    /// Extensions to help with maintainence of a list
    /// </summary>
    public static class ListEx
    {
        #region Apply operators to a list

        internal static bool MovedWithinRange<T>(this Change<T> source, int startIndex, int endIndex)
        {
            if (source.Reason != ListChangeReason.Moved)
                return false;

            var current = source.Item.CurrentIndex;
            var previous = source.Item.PreviousIndex;

            return current >= startIndex && current <= endIndex
                   || previous >= startIndex && previous <= endIndex;
        }

        /// <summary>
        /// Clones the list from the specified change set
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="changes">The changes.</param>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// changes
        /// </exception>
        public static void Clone<T>(this IList<T> source, IChangeSet<T> changes)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (changes == null) throw new ArgumentNullException(nameof(changes));

            foreach (var item in changes)
            {
                Clone(source,  item);
            }
        }

        private static void Clone<T>(this IList<T> source,  Change<T> item)
        {
            var changeAware = source as ChangeAwareList<T>;

            switch (item.Reason)
            {
                case ListChangeReason.Add:
                {
                    var change = item.Item;
                    var hasIndex = change.CurrentIndex >= 0;
                    if (hasIndex)
                    {
                        source.Insert(change.CurrentIndex, change.Current);
                    }
                    else
                    {
                        source.Add(change.Current);
                    }
                    break;
                }
                case ListChangeReason.AddRange:
                {
                    source.AddOrInsertRange(item.Range, item.Range.Index);
                    break;
                }
                case ListChangeReason.Clear:
                {
                    source.ClearOrRemoveMany(item);
                    break;
                }
                case ListChangeReason.Replace:
                {
                    var change = item.Item;
                    if (change.CurrentIndex >= 0 && change.CurrentIndex == change.PreviousIndex)
                    {
                        source[change.CurrentIndex] = change.Current;
                    }
                    else
                    {
                        if (change.PreviousIndex == -1)
                        {
                            source.Remove(change.Previous.Value);
                        }
                        else
                        {
                            //is this best? or replace + move?
                            source.RemoveAt(change.PreviousIndex);
                        }

                        if (change.CurrentIndex == -1)
                        {
                            source.Add(change.Current);
                        }
                        else
                        {
                            source.Insert(change.CurrentIndex, change.Current);
                        }


                    }

                    break;
                }
                case ListChangeReason.Refresh:
                {
                    if (changeAware != null)
                    {
                        changeAware.RefreshAt(item.Item.CurrentIndex);
                        }

                    else
                    {
                        source.RemoveAt(item.Item.CurrentIndex);
                        source.Insert(item.Item.CurrentIndex, item.Item.Current);
                    }

                    break;
                }
                case ListChangeReason.Remove:
                {
                    var change = item.Item;
                    bool hasIndex = change.CurrentIndex >= 0;
                    if (hasIndex)
                    {
                        source.RemoveAt(change.CurrentIndex);
                    }
                    else
                    {
                        source.Remove(change.Current);
                    }

                    break;
                }
                case ListChangeReason.RemoveRange:
                {
                    //ignore this case because WhereReasonsAre removes the index [in which case call RemoveMany]
                    //if (item.Range.Index < 0)
                    //    throw new UnspecifiedIndexException("ListChangeReason.RemoveRange should not have an index specified index");

                    if (item.Range.Index >= 0 && (source is IExtendedList<T> || source is List<T>))
                    {
                        source.RemoveRange(item.Range.Index, item.Range.Count);
                    }
                    else
                    {
                        source.RemoveMany(item.Range);
                    }

                    break;
                }
                case ListChangeReason.Moved:
                {
                    var change = item.Item;
                    bool hasIndex = change.CurrentIndex >= 0;
                    if (!hasIndex)
                        throw new UnspecifiedIndexException("Cannot move as an index was not specified");

                    var extendedList = source as IExtendedList<T>;
                    var observableCollection = source as ObservableCollection<T>;
                    if (extendedList != null)
                    {
                        extendedList.Move(change.PreviousIndex, change.CurrentIndex);
                    }
                    else if (observableCollection != null)
                    {
                        observableCollection.Move(change.PreviousIndex, change.CurrentIndex);
                    }
                    else
                    {
                        //check this works whatever the index is
                        source.RemoveAt(change.PreviousIndex);
                        source.Insert(change.CurrentIndex, change.Current);
                    }
                    break;
                }
            }
        }

        /// <summary>
        /// Clears the collection if the number of items in the range is the same as the source collection. Otherwise a  remove many operation is applied.
        ///
        /// NB: This is because an observable change set may be a composite of multiple change sets in which case if one of them has clear operation applied it should not clear the entire result.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="change">The change.</param>
        internal static void ClearOrRemoveMany<T>(this IList<T> source, Change<T> change)
        {
            //apply this to other operators
            if (source.Count == change.Range.Count)
            {
                source.Clear();
            }
            else
            {
                source.RemoveMany(change.Range);
            }
        }

        #endregion

        #region Binary Search / Lookup

        /// <summary>
        /// Performs a binary search on the specified collection.
        /// </summary>
        /// <typeparam name="TItem">The type of the item.</typeparam>
        /// <param name="list">The list to be searched.</param>
        /// <param name="value">The value to search for.</param>
        /// <returns></returns>
        public static int BinarySearch<TItem>(this IList<TItem> list, TItem value)
        {
            return BinarySearch(list, value, Comparer<TItem>.Default);
        }

        /// <summary>
        /// Performs a binary search on the specified collection.
        /// </summary>
        /// <typeparam name="TItem">The type of the item.</typeparam>
        /// <param name="list">The list to be searched.</param>
        /// <param name="value">The value to search for.</param>
        /// <param name="comparer">The comparer that is used to compare the value with the list items.</param>
        /// <returns></returns>
        public static int BinarySearch<TItem>(this IList<TItem> list, TItem value, IComparer<TItem> comparer)
        {
            return list.BinarySearch(value, comparer.Compare);
        }

        /// <summary>
        /// Performs a binary search on the specified collection.
        ///
        /// Thanks to http://stackoverflow.com/questions/967047/how-to-perform-a-binary-search-on-ilistt
        /// </summary>
        /// <typeparam name="TItem">The type of the item.</typeparam>
        /// <typeparam name="TSearch">The type of the searched item.</typeparam>
        /// <param name="list">The list to be searched.</param>
        /// <param name="value">The value to search for.</param>
        /// <param name="comparer">The comparer that is used to compare the value with the list items.</param>
        /// <returns></returns>
        public static int BinarySearch<TItem, TSearch>(this IList<TItem> list, TSearch value, Func<TSearch, TItem, int> comparer)
        {
            if (list == null)
                throw new ArgumentNullException(nameof(list));

            if (comparer == null)
                throw new ArgumentNullException(nameof(comparer));

            int lower = 0;
            int upper = list.Count - 1;

            while (lower <= upper)
            {
                int middle = lower + (upper - lower) / 2;
                int comparisonResult = comparer(value, list[middle]);
                if (comparisonResult < 0)
                {
                    upper = middle - 1;
                }
                else if (comparisonResult > 0)
                {
                    lower = middle + 1;
                }
                else
                {
                    return middle;
                }
            }

            return ~lower;
        }

        /// <summary>
        /// Lookups the item using the specified comparer. If matched, the item's index is also returned
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="item">The item.</param>
        /// <param name="equalityComparer">The equality comparer.</param>
        /// <returns></returns>
        public static Optional<ItemWithIndex<T>> IndexOfOptional<T>(this IEnumerable<T> source, T item, IEqualityComparer<T> equalityComparer = null)
        {
            var comparer = equalityComparer ?? EqualityComparer<T>.Default;
            var index = source.IndexOf(item, comparer);
            return index<0 ? Optional<ItemWithIndex<T>>.None : new ItemWithIndex<T>(item,index);
        }

        /// <summary>
        /// Finds the index of the current item using the specified equality comparer
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="item"></param>
        /// <param name="source"></param>
        /// <returns></returns>
        public static int IndexOf<T>(this IEnumerable<T> source, T item)
        {
            return IndexOf<T>(source, item, EqualityComparer<T>.Default);
        }

        /// <summary>
        /// Finds the index of the current item using the specified equality comparer
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="item"></param>
        /// <param name="equalityComparer">Use to determine object equality</param>
        /// <param name="source"></param>
        /// <returns></returns>
        public static int IndexOf<T>(this IEnumerable<T> source, T item, IEqualityComparer<T> equalityComparer)
        {
            int i = 0;
            foreach (var candidate in source)
            {
                if (equalityComparer.Equals(item, candidate))
                    return i;

                i++;
            }
            return -1;
        }


        #endregion

        #region Amendment

        /// <summary>
        /// Adds the  items to the specified list
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="items">The items.</param>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// items
        /// </exception>
        public static void Add<T>(this IList<T> source, IEnumerable<T> items)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (items == null) throw new ArgumentNullException(nameof(items));

            items.ForEach(source.Add);
        }

        /// <summary>
        /// Adds the range to the source ist
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="items">The items.</param>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// items
        /// </exception>
        public static void AddRange<T>(this IList<T> source, IEnumerable<T> items)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (items == null) throw new ArgumentNullException(nameof(items));

            if (source is List<T>)
            {
                ((List<T>)source).AddRange(items);
            }
            else if (source is IExtendedList<T>)
            {
                ((IExtendedList<T>)source).AddRange(items);
            }
            else
            {
                items.ForEach(source.Add);
            }
        }

        /// <summary>
        /// Adds the range to the list. The starting range is at the specified index
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="items">The items.</param>
        /// <param name="index">The index.</param>
        /// <exception cref="System.ArgumentNullException">
        /// </exception>
        public static void AddRange<T>(this IList<T> source, IEnumerable<T> items, int index)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (items == null) throw new ArgumentNullException(nameof(items));

            if (source is List<T>)
            {
                ((List<T>)source).InsertRange(index, items);
            }
            else if (source is IExtendedList<T>)
            {
                ((IExtendedList<T>)source).InsertRange(items, index);
            }
            else
            {
                items.ForEach(source.Add);
            }
        }

        /// <summary>
        /// Adds the range if a negative is specified, otherwise the range is added at the end of the list
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="items">The items.</param>
        /// <param name="index">The index.</param>
        /// <exception cref="System.ArgumentNullException">
        /// </exception>
        public static void AddOrInsertRange<T>(this IList<T> source, IEnumerable<T> items, int index)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (items == null) throw new ArgumentNullException(nameof(items));

            if (source is List<T>)
            {
                if (index >= 0)
                {
                    ((List<T>)source).InsertRange(index, items);
                }
                else
                {
                    ((List<T>)source).AddRange(items);
                }
            }
            else if (source is IExtendedList<T>)
            {
                if (index >= 0)
                {
                    ((IExtendedList<T>)source).InsertRange(items, index);
                }
                else
                {
                    ((IExtendedList<T>)source).AddRange(items);
                }
            }
            else
            {
                if (index >= 0)
                {
                    //TODO: Why the hell reverse? Surely there must be as reason otherwise I would not have done it.
                    items.Reverse().ForEach(t => source.Insert(index, t));
                }
                else
                {
                    items.ForEach(source.Add);
                }
            }
        }

        /// <summary>
        /// Removes many items from the collection in an optimal way
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="itemsToRemove">The items to remove.</param>
        /// <exception cref="System.ArgumentNullException">
        /// </exception>
        public static void RemoveMany<T>(this IList<T> source, [NotNull] IEnumerable<T> itemsToRemove)
        {
            /*
                This may seem OTT but for large sets of data where there are many removes scattered
                across the source collection IndexOf lookups can result in very slow updates
                (especially for subsequent operators)
            */
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (itemsToRemove == null) throw new ArgumentNullException(nameof(itemsToRemove));

            var toRemoveArray = itemsToRemove.AsArray();

            //match all indicies and and remove in reverse as it is more efficient
            var toRemove = source.IndexOfMany(toRemoveArray)
                                 .OrderByDescending(x => x.Index)
                                 .ToArray();

            //if there are duplicates, it could be that an item exists in the
            //source collection more than once - in that case the fast remove
            //would remove each instance
            var hasDuplicates = toRemove.Duplicates(t => t.Item).Any();

            if (hasDuplicates)
            {
                //Slow remove but safe
                toRemoveArray.ForEach(t => source.Remove(t));
            }
            else
            {
                //Fast remove because we know the index of all and we remove in order
                toRemove.ForEach(t => source.RemoveAt(t.Index));
            }
        }

        /// <summary>
        /// Removes the number of items, starting at the specified index
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="index">The index.</param>
        /// <param name="count">The count.</param>
        /// <exception cref="System.ArgumentNullException"></exception>
        /// <exception cref="System.NotSupportedException">Cannot remove range</exception>
        private static void RemoveRange<T>(this IList<T> source, int index, int count)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            if (source is List<T>)
            {
                ((List<T>)source).RemoveRange(index, count);
            }
            else if (source is IExtendedList<T>)
            {
                ((IExtendedList<T>)source).RemoveRange(index, count);
            }
            else
            {
                throw new NotSupportedException($"Cannot remove range from {source.GetType().FullName}");
            }
        }

        /// <summary>
        /// Removes the  items from the specified list
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="items">The items.</param>
        /// <exception cref="System.ArgumentNullException">
        /// source
        /// or
        /// items
        /// </exception>
        public static void Remove<T>(this IList<T> source, IEnumerable<T> items)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (items == null) throw new ArgumentNullException(nameof(items));

            items.ForEach(t => source.Remove(t));
        }

        /// <summary>
        /// Replaces the specified item.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="original">The original.</param>
        /// <param name="replacewith">The replacewith.</param>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// items</exception>
        public static void Replace<T>(this IList<T> source, [NotNull] T original, [NotNull] T replacewith)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (original == null) throw new ArgumentNullException(nameof(original));
            if (replacewith == null) throw new ArgumentNullException(nameof(replacewith));

            var index = source.IndexOf(original);
            if (index==-1)
                throw new ArgumentException("Cannot find index of original item. Either it does not exist in the list or the hashcode has mutated");
            source[index] = replacewith;
        }


        /// <summary>
        /// Replaces the item if found, otherwise the item is added to the list
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="original">The original.</param>
        /// <param name="replacewith">The replacewith.</param>
        /// <exception cref="System.ArgumentNullException">
        /// </exception>
        public static void ReplaceOrAdd<T>(this IList<T> source, [NotNull] T original, [NotNull] T replacewith)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (original == null) throw new ArgumentNullException(nameof(original));
            if (replacewith == null) throw new ArgumentNullException(nameof(replacewith));

            var index = source.IndexOf(original);
            if (index == -1)
            {
                source.Add(replacewith);
            }
            else
            {
                source[index] = replacewith;
            }

        }

        /// <summary>
        /// Replaces the specified item.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source">The source.</param>
        /// <param name="original">The original.</param>
        /// <param name="replacewith">The replacewith.</param>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// items</exception>
        public static void Replace<T>(this IList<T> source, [NotNull] T original, [NotNull] T replacewith, IEqualityComparer<T> comparer)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (original == null) throw new ArgumentNullException(nameof(original));
            if (replacewith == null) throw new ArgumentNullException(nameof(replacewith));
            if (comparer == null) throw new ArgumentNullException(nameof(comparer));

            var index = source.IndexOf(original);
            if (index == -1)
                throw new ArgumentException("Cannot find index of original item. Either it does not exist in the list or the hashcode has mutated");
            if (comparer.Equals(source[index], replacewith))
                source[index] = replacewith;
        }

        #endregion
    }
}
