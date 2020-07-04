// Copyright (c) 2011-2019 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using DynamicData.Annotations;

namespace DynamicData.Cache.Internal
{
    internal class RemoveKeyEnumerator<TObject, TKey> : IEnumerable<Change<TObject>>
    {
        private readonly IChangeSet<TObject, TKey> _source;
        private readonly IExtendedList<TObject> _list;

        /// <summary>Converts a <see cref="Change{TObject, TKey}"/> to <see cref="ChangeSet{TObject}"/></summary>
        /// <param name="source">The changeset with a key</param>
        /// <param name="list">
        /// An optional list, if provided it allows the refresh from a key based cache to find the index for the resulting list based refresh.
        /// If not provided a refresh will dropdown to a replace which may ultimately result in a remove+add change downstream.
        /// </param>
        public RemoveKeyEnumerator([NotNull] IChangeSet<TObject, TKey> source, [CanBeNull] IExtendedList<TObject> list = null)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _list = list;
        }

        /// <summary>
        /// Returns an enumerator that iterates through the collection.
        /// </summary>
        /// <returns>
        /// A <see cref="T:System.Collections.Generic.IEnumerator`1" /> that can be used to iterate through the collection.
        /// </returns>
        /// <exception cref="System.ArgumentOutOfRangeException"></exception>
        public IEnumerator<Change<TObject>> GetEnumerator()
        {
            foreach (var change in _source)
            {
                switch (change.Reason)
                {
                    case ChangeReason.Add:
                        yield return new Change<TObject>(ListChangeReason.Add, change.Current, change.CurrentIndex);
                        break;
                    case ChangeReason.Refresh:
                        // Refresh needs an index, which we don't have in a Change<T, K> model since it's key based.
                        // See: DynamicData > Binding > ObservableCollectionAdaptor.cs Line 129-130

                        // Note: A refresh is not index based within the context of a sorted change.
                        // Thus, currentIndex will not be available here where as other changes like add and remove do have indexes if coming from a sorted changeset.

                        // In order to properly handle a refresh and map to an index on a list, we need to use the source list (within the edit method so that it's thread safe)
                        if (_list != null && _list.IndexOf(change.Current) is int index && index >= 0)
                        {
                            yield return new Change<TObject>(ListChangeReason.Refresh, current: change.Current, index: index);
                        }
                        // Fallback to a replace if a list is not available
                        else
                        {
                            yield return new Change<TObject>(ListChangeReason.Replace, current: change.Current, previous: change.Current);
                        }
                        break;
                    case ChangeReason.Moved:
                        // Move is always sorted
                        yield return new Change<TObject>(change.Current, change.CurrentIndex, change.PreviousIndex);
                        break;
                    case ChangeReason.Update:
                        // If not sorted
                        if (change.CurrentIndex == -1)
                        {
                            yield return new Change<TObject>(ListChangeReason.Remove, change.Previous.Value);
                            yield return new Change<TObject>(ListChangeReason.Add, change.Current);
                        }
                        else
                        {
                            yield return new Change<TObject>(ListChangeReason.Remove, change.Current, index: change.CurrentIndex);
                            yield return new Change<TObject>(ListChangeReason.Add, change.Current, index: change.CurrentIndex);
                        }
                        break;
                    case ChangeReason.Remove:
                        // If not sorted
                        if (change.CurrentIndex == -1)
                        {
                            yield return new Change<TObject>(ListChangeReason.Remove, change.Current);
                        }
                        else
                        {
                            yield return new Change<TObject>(ListChangeReason.Remove, change.Current, index: change.CurrentIndex);
                        }
                        break;
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
