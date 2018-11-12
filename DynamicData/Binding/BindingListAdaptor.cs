#if SUPPORTS_BINDINGLIST

using System;
using System.ComponentModel;
using DynamicData.Annotations;
using DynamicData.Cache.Internal;

namespace DynamicData.Binding
{
    /// <summary>
    /// Adaptor to reflect a change set into a binding list
    /// </summary>
    public class BindingListAdaptor<T> : IChangeSetAdaptor<T>
    {
        private readonly BindingList<T> _list;
        private readonly int _refreshThreshold;
        private bool _loaded;

        /// <inheritdoc />
        public BindingListAdaptor([NotNull] BindingList<T> list, int refreshThreshold = 25)
        {
            _list = list ?? throw new ArgumentNullException(nameof(list));
            _refreshThreshold = refreshThreshold;
        }

        /// <inheritdoc />
        public void Adapt(IChangeSet<T> changes)
        {
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
    /// Adaptor to reflect a change set into a binding list
    /// </summary>
    public class BindingListAdaptor<TObject, TKey> : IChangeSetAdaptor<TObject, TKey>
    {
        private readonly BindingList<TObject> _list;
        private readonly int _refreshThreshold;
        private bool _loaded;

        private readonly Cache<TObject, TKey> _cache = new Cache<TObject, TKey>();

        /// <inheritdoc />
        public BindingListAdaptor([NotNull] BindingList<TObject> list, int refreshThreshold = 25)
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

        private void DoUpdate(IChangeSet<TObject, TKey> changes, BindingList<TObject> list)
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
                        list.Remove(update.Previous.Value);
                        list.Add(update.Current);
                        break;
                }
            }
        }
    }
}

#endif