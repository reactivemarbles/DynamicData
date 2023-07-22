﻿// Copyright (c) 2011-2020 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if SUPPORTS_BINDINGLIST
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

using DynamicData.Cache.Internal;

namespace DynamicData.Binding
{
    /// <summary>
    /// Adaptor to reflect a change set into a binding list.
    /// </summary>
    /// <typeparam name="T">The type of items.</typeparam>
    public class BindingListAdaptor<T> : IChangeSetAdaptor<T>
        where T : notnull
    {
        private readonly BindingList<T> _list;

        private readonly int _refreshThreshold;

        private bool _loaded;

        /// <summary>
        /// Initializes a new instance of the <see cref="BindingListAdaptor{T}"/> class.
        /// </summary>
        /// <param name="list">The list of items to add to the adapter.</param>
        /// <param name="refreshThreshold">The threshold before a reset is issued.</param>
        public BindingListAdaptor(BindingList<T> list, int refreshThreshold = 25)
        {
            _list = list ?? throw new ArgumentNullException(nameof(list));
            _refreshThreshold = refreshThreshold;
        }

        /// <inheritdoc />
        public void Adapt(IChangeSet<T> changes)
        {
            if (changes is null)
            {
                throw new ArgumentNullException(nameof(changes));
            }

            if (changes.TotalChanges - changes.Refreshes > _refreshThreshold || !_loaded)
            {
                using (new BindingListEventsSuspender<T>(_list))
                {
                    _list.Clone(changes);
                    _loaded = true;
                }
            }
            else
            {
                _list.Clone(changes);
            }
        }
    }

    /// <summary>
    /// Adaptor to reflect a change set into a binding list.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "Same class name, different generics")]
    public class BindingListAdaptor<TObject, TKey> : IChangeSetAdaptor<TObject, TKey>
        where TObject : notnull
        where TKey : notnull
    {
        private readonly Cache<TObject, TKey> _cache = new();

        private readonly BindingList<TObject> _list;

        private readonly int _refreshThreshold;

        private bool _loaded;

        /// <summary>
        /// Initializes a new instance of the <see cref="BindingListAdaptor{TObject, TKey}"/> class.
        /// </summary>
        /// <param name="list">The list of items to adapt.</param>
        /// <param name="refreshThreshold">The threshold before the refresh is triggered.</param>
        public BindingListAdaptor(BindingList<TObject> list, int refreshThreshold = 25)
        {
            _list = list ?? throw new ArgumentNullException(nameof(list));
            _refreshThreshold = refreshThreshold;
        }

        /// <inheritdoc />
        public void Adapt(IChangeSet<TObject, TKey> changes)
        {
            _cache.Clone(changes);

            if (changes.Count - changes.Refreshes > _refreshThreshold || !_loaded)
            {
                using (new BindingListEventsSuspender<TObject>(_list))
                {
                    _list.Clear();
                    _list.AddRange(_cache.Items);
                    _loaded = true;
                }
            }
            else
            {
                DoUpdate(changes, _list);
            }
        }

        private static void DoUpdate(IChangeSet<TObject, TKey> changes, BindingList<TObject> list)
        {
            foreach (var update in changes)
            {
                switch (update.Reason)
                {
                    case ChangeReason.Add:
                        list.Add(update.Current);
                        break;

                    case ChangeReason.Remove:
                        list.Remove(update.Current);
                        break;

                    case ChangeReason.Update:
                        var previousIndex = list.IndexOf(update.Previous.Value);
                        if (previousIndex >= 0)
                            list[previousIndex] = update.Current;
                        else
                            list.Add(update.Current);
                        break;

                    case ChangeReason.Refresh:
                        var index = list.IndexOf(update.Current);
                        if (index != -1)
                            list.ResetItem(index);
                        break;
                }
            }
        }
    }
}

#endif
