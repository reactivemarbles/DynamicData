using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Annotations;
using DynamicData.Kernel;

namespace DynamicData.List.Internal
{
    internal sealed class OnBeingRemoved<T>
    {
        private readonly IObservable<IChangeSet<T>> _source;
        private readonly Action<T> _callback;

        public OnBeingRemoved([NotNull] IObservable<IChangeSet<T>> source, [NotNull] Action<T> callback)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _callback = callback ?? throw new ArgumentNullException(nameof(callback));
        }

        public IObservable<IChangeSet<T>> Run()
        {
            return Observable.Create<IChangeSet<T>>(observer =>
                    {
                        var items = new List<T>();
                        var subscriber = _source
                            .Do(changes => RegisterForRemoval(items, changes), observer.OnError)
                            .SubscribeSafe(observer);

                        return Disposable.Create(() =>
                        {
                            subscriber.Dispose();
                            items.ForEach(t => _callback(t));
                            items.Clear();
                        });
                    });
        }

        private void RegisterForRemoval(IList<T> items, IChangeSet<T> changes)
        {
            foreach (var change in changes)
            {
                switch (change.Reason)
                {
                    case ListChangeReason.Replace:
                        change.Item.Previous.IfHasValue(t => _callback(t));
                        break;
                    case ListChangeReason.Remove:
                        _callback(change.Item.Current);
                        break;
                    case ListChangeReason.RemoveRange:
                        change.Range.ForEach(_callback);
                        break;
                    case ListChangeReason.Clear:
                        items.ForEach(_callback);
                        break;
                }
            }
            items.Clone(changes);
        }
    }
}
