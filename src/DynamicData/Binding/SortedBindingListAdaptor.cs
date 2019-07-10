// Copyright (c) 2011-2019 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if SUPPORTS_BINDINGLIST

using System;
using System.ComponentModel;
using System.Linq;
using DynamicData.Annotations;

namespace DynamicData.Binding
{
    /// <summary>
    /// Represents an adaptor which is used to update a binding list from
    /// a sorted change set
    /// </summary>
    public class SortedBindingListAdaptor<TObject, TKey> : ISortedChangeSetAdaptor<TObject, TKey>
    {
        private readonly BindingList<TObject> _list;
        private readonly int _refreshThreshold;

        /// <inheritdoc />
        public SortedBindingListAdaptor([NotNull] BindingList<TObject> list, int refreshThreshold = 25)
        {
            _list = list ?? throw new ArgumentNullException(nameof(list));
            _refreshThreshold = refreshThreshold;
        }

        /// <inheritdoc />
        public void Adapt(ISortedChangeSet<TObject, TKey> changes)
        {
            if (changes == null)
            {
                throw new ArgumentNullException(nameof(changes));
            }

            switch (changes.SortedItems.SortReason)
            {
                case SortReason.InitialLoad:
                case SortReason.ComparerChanged:
                case SortReason.Reset:
                    using (new BindingListEventsSuspender<TObject>(_list))
                    {
                        _list.Clear();
                        _list.AddRange(changes.SortedItems.Select(kv => kv.Value));
                    }

                    break;

                case SortReason.DataChanged:
                    if (changes.Count - changes.Refreshes > _refreshThreshold)
                    {
                        using (new BindingListEventsSuspender<TObject>(_list))
                        {
                            _list.Clear();
                            _list.AddRange(changes.SortedItems.Select(kv => kv.Value));
                        }
                    }
                    else
                    {
                        DoUpdate(changes);
                    }

                    break;

                case SortReason.Reorder:
                    DoUpdate(changes);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(changes));
            }
        }

        private void DoUpdate(ISortedChangeSet<TObject, TKey> changes)
        {
            foreach (var change in changes)
            {
                switch (change.Reason)
                {
                    case ChangeReason.Add:
                        _list.Insert(change.CurrentIndex, change.Current);
                        break;
                    case ChangeReason.Remove:
                        _list.RemoveAt(change.CurrentIndex);
                        break;
                    case ChangeReason.Moved:
                        _list.RemoveAt(change.PreviousIndex);
                        _list.Insert(change.CurrentIndex, change.Current);
                        break;
                    case ChangeReason.Update:
                        _list.RemoveAt(change.PreviousIndex);
                        _list.Insert(change.CurrentIndex, change.Current);
                        break;
                }
            }
        }
    }
}
#endif