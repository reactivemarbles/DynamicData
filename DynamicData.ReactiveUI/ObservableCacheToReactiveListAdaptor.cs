using System;
using System.Collections.Generic;
using ReactiveUI;

namespace DynamicData.ReactiveUI
{
    /// <summary>
    /// Adaptor used to populate a <see cref="ReactiveList{TObject}"/> from an observable changeset.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    internal class ObservableCacheToReactiveListAdaptor<TObject, TKey> : IChangeSetAdaptor<TObject, TKey>
    {
        private IDictionary<TKey, TObject> _data;
        private bool _loaded;
        private readonly IReactiveList<TObject> _target;
        private readonly int _resetThreshold;

        /// <summary>
        /// Initializes a new instance of the <see cref="ObservableCacheToReactiveListAdaptor{TObject,TKey}"/> class.
        /// </summary>
        /// <param name="target">The target.</param>
        /// <param name="resetThreshold">The reset threshold.</param>
        /// <exception cref="System.ArgumentNullException">target</exception>
        public ObservableCacheToReactiveListAdaptor(ReactiveList<TObject> target, int resetThreshold = 50)
        {
            _target = target ?? throw new ArgumentNullException(nameof(target));
            _resetThreshold = resetThreshold;
        }

        /// <summary>
        /// Adapts the specified changeset
        /// </summary>
        /// <param name="changes">The changes.</param>
        public void Adapt(IChangeSet<TObject, TKey> changes)
        {
            Clone(changes);

            if (changes.Count > _resetThreshold || !_loaded)
            {
                _loaded = true;
                using (_target.SuppressChangeNotifications())
                {
                    _target.Clear();
                    _target.AddRange(_data.Values);
                }
            }
            else
            {
                DoUpdate(changes);
            }
        }

        private void Clone(IChangeSet<TObject, TKey> changes)
        {
            //for efficiency resize dictionary to initial batch size
            if (_data == null || _data.Count == 0)
                _data = new Dictionary<TKey, TObject>(changes.Count);

            foreach (var item in changes)
            {
                switch (item.Reason)
                {
                    case ChangeReason.Update:
                    case ChangeReason.Add:
                    {
                        _data[item.Key] = item.Current;
                    }
                        break;
                    case ChangeReason.Remove:
                        _data.Remove(item.Key);
                        break;
                }
            }
        }

        private void DoUpdate(IChangeSet<TObject, TKey> changes)
        {
            foreach (var change in changes)
            {
                switch (change.Reason)
                {
                    case ChangeReason.Add:
                        _target.Add(change.Current);
                        break;
                    case ChangeReason.Remove:
                        _target.Remove(change.Current);
                        break;
                    case ChangeReason.Update:
                    {
                        _target.Remove(change.Previous.Value);
                        _target.Add(change.Current);
                    }
                        break;
                }
            }

        }
    }
}