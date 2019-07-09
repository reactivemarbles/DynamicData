using System.Collections.Generic;
using DynamicData.Kernel;
using DynamicData.List.Internal;

// ReSharper disable once CheckNamespace
namespace DynamicData
{
    internal class ChangeAwareListWithRefCounts<T> : ChangeAwareList<T>
    {
        private readonly ReferenceCountTracker<T> _tracker = new ReferenceCountTracker<T>();

        protected override void InsertItem(int index, T item)
        {
            _tracker.Add(item);
            base.InsertItem(index, item);
        }

        protected override void OnInsertItems(int startIndex, IEnumerable<T> items)
        {
            items.ForEach(t => _tracker.Add(t));
        }

        protected override void RemoveItem(int index, T item)
        {
            _tracker.Remove(item);
            base.RemoveItem(index, item);
        }

        protected override void OnRemoveItems(int startIndex, IEnumerable<T> items)
        {
            items.ForEach(t => _tracker.Remove(t));
        }

        protected override void OnSetItem(int index, T newItem, T oldItem)
        {
            _tracker.Remove(oldItem);
            _tracker.Add(newItem);
            base.OnSetItem(index, newItem, oldItem);
        }

        public override bool Contains(T item)
        {
            return _tracker.Contains(item);
        }

        public override void Clear()
        {
            _tracker.Clear();
            base.Clear();
        }
    }
}
