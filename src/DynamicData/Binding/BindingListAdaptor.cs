// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if SUPPORTS_BINDINGLIST
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using DynamicData.Cache;
using DynamicData.Cache.Internal;

namespace DynamicData.Binding
{
    /// <summary>
    /// Adaptor to reflect a change set into a binding list.
    /// </summary>
    /// <typeparam name="T">The type of items.</typeparam>
    /// <remarks>
    /// Initializes a new instance of the <see cref="BindingListAdaptor{T}"/> class.
    /// </remarks>
    /// <param name="list">The list of items to add to the adapter.</param>
    /// <param name="refreshThreshold">The threshold before a reset is issued.</param>
    public class BindingListAdaptor<T>(BindingList<T> list, int refreshThreshold = BindingOptions.DefaultResetThreshold) : IChangeSetAdaptor<T>
        where T : notnull
    {
        private readonly BindingList<T> _list = list ?? throw new ArgumentNullException(nameof(list));
        private bool _loaded;

        /// <inheritdoc />
        public void Adapt(IChangeSet<T> changes)
        {
            changes.ThrowArgumentNullExceptionIfNull(nameof(changes));

            if (changes.TotalChanges - changes.Refreshes > refreshThreshold || !_loaded)
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
    /// <remarks>
    /// Initializes a new instance of the <see cref="BindingListAdaptor{TObject, TKey}"/> class.
    /// </remarks>
    /// <param name="list">The list of items to adapt.</param>
    /// <param name="refreshThreshold">The threshold before the refresh is triggered.</param>
    [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "Same class name, different generics")]
    public class BindingListAdaptor<TObject, TKey>(BindingList<TObject> list, int refreshThreshold = BindingOptions.DefaultResetThreshold) : IChangeSetAdaptor<TObject, TKey>
        where TObject : notnull
        where TKey : notnull
    {
        private readonly Cache<TObject, TKey> _cache = new();

        private readonly BindingList<TObject> _list = list ?? throw new ArgumentNullException(nameof(list));
        private bool _loaded;

        /// <inheritdoc />
        public void Adapt(IChangeSet<TObject, TKey> changes)
        {
            changes.ThrowArgumentNullExceptionIfNull(nameof(changes));
            _cache.Clone(changes);

            if (changes.Count - changes.Refreshes > refreshThreshold || !_loaded)
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
            foreach (var update in changes.ToConcreteType())
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
                        {
                            list[previousIndex] = update.Current;
                        }
                        else
                        {
                            list.Add(update.Current);
                        }

                        break;

                    case ChangeReason.Refresh:
                        var index = list.IndexOf(update.Current);
                        if (index != -1)
                        {
                            list.ResetItem(index);
                        }

                        break;
                }
            }
        }
    }
}

#endif
