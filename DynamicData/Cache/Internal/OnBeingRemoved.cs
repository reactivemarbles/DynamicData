using System;
using DynamicData.Kernel;

namespace DynamicData.Internal
{
    internal sealed class OnBeingRemoved<TObject, TKey> : IDisposable
    {
        private readonly Action<TObject> _callback;
        private readonly Cache<TObject, TKey> _cache = new Cache<TObject, TKey>();

        public OnBeingRemoved(Action<TObject> callback)
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            _callback = callback;
        }

        public void RegisterForRemoval(IChangeSet<TObject, TKey> changes)
        {
            changes.ForEach(change =>
            {
                switch (change.Reason)
                {
                    case ChangeReason.Update:
                        change.Previous.IfHasValue(t => _callback(t));
                        break;
                    case ChangeReason.Remove:
                        _callback(change.Current);
                        break;
                }
            });
            _cache.Clone(changes);
        }

        public void Dispose()
        {
            _cache.Items.ForEach(t => _callback(t));
            _cache.Clear();
        }
    }
}
