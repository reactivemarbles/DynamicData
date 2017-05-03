using System;
using System.Reactive.Linq;
using DynamicData.Kernel;

namespace DynamicData.List.Internal
{
    internal sealed class OnBeingAdded<T>
    {
        private readonly IObservable<IChangeSet<T>> _source;
        private readonly Action<T> _callback;

        public OnBeingAdded(IObservable<IChangeSet<T>> source, Action<T> callback)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _callback = callback ?? throw new ArgumentNullException(nameof(callback));
        }

        public IObservable<IChangeSet<T>> Run()
        {
            return _source.Do(RegisterForAddition);
        }

        private void RegisterForAddition(IChangeSet<T> changes)
        {
            foreach (var change in changes)
            {
                switch (change.Reason)
                {
                    case ListChangeReason.Add:
                        _callback(change.Item.Current);
                        break;
                    case ListChangeReason.AddRange:
                        change.Range.ForEach(_callback);
                        break;
                    case ListChangeReason.Replace:
                        _callback(change.Item.Current);
                        break;
                }
            }
        }
    }
}
