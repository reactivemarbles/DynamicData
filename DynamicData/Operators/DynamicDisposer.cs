using System;
using System.Linq;
using System.Reactive.Linq;
using DynamicData.Kernel;

namespace DynamicData.Operators
{
    internal sealed class DynamicDisposer<TObject, TKey> : IDisposable
    {
        private readonly Cache<TObject, TKey> _cache = new Cache<TObject, TKey>();
        private readonly CacheCloner<TObject, TKey> _updater;

        public DynamicDisposer()
        {
            _updater = new CacheCloner<TObject, TKey>(_cache);
        }

        public void Dispose()
        {
            _cache.Items.OfType<IDisposable>().ForEach(t => t.Dispose());
            _cache.Clear();
        }

        public void SetForDisposable(IChangeSet<TObject, TKey> updates)
        {
            foreach (var update in updates)
            {
                TObject current = update.Current;
                Optional<TObject> previous = update.Previous;

                switch (update.Reason)
                {
                    case ChangeReason.Update:
                    {
                        if (previous.HasValue)
                        {
                            var disposable = update.Previous.Value as IDisposable;
                            if (disposable != null) disposable.Dispose();
                        }
                    }

                        break;
                    case ChangeReason.Remove:
                    {
                        var disposable = current as IDisposable;
                        if (disposable != null) disposable.Dispose();
                        
                    }
                        break;
                }
            }
            _updater.Clone(updates);
        }
    }
}