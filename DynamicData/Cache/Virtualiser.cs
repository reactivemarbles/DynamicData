using System;
using System.Linq;
using System.Reactive.Linq;
using DynamicData.Cache.Internal;

namespace DynamicData.Cache
{
    internal sealed class Virtualise<TObject, TKey>
    {
        private readonly IObservable<ISortedChangeSet<TObject, TKey>> _source;
        private readonly IObservable<IVirtualRequest> _virtualRequests;

        public Virtualise(IObservable<ISortedChangeSet<TObject, TKey>> source,
            IObservable<IVirtualRequest> virtualRequests)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _virtualRequests = virtualRequests ?? throw new ArgumentNullException(nameof(virtualRequests));
        }

        public IObservable<IVirtualChangeSet<TObject, TKey>> Run()
        {
            return Observable.Create<IVirtualChangeSet<TObject, TKey>>(observer =>
            {
                var virtualiser = new Virtualiser();
                var locker = new object();

                var request = _virtualRequests.Synchronize(locker).Select(virtualiser.Virtualise);
                var datachange = _source.Synchronize(locker).Select(virtualiser.Update);
                return request.Merge(datachange)
                    .Where(updates => updates != null)
                    .SubscribeSafe(observer);
            });
        }

        internal sealed class Virtualiser
        {
            private readonly FilteredIndexCalculator<TObject, TKey> _changedCalculator = new FilteredIndexCalculator<TObject, TKey>();
            private IKeyValueCollection<TObject, TKey> _all = new KeyValueCollection<TObject, TKey>();
            private IKeyValueCollection<TObject, TKey> _current = new KeyValueCollection<TObject, TKey>();
            private IVirtualRequest _parameters;
            private bool _isLoaded;

            public Virtualiser(VirtualRequest request = null)
            {
                _parameters = request ?? new VirtualRequest();
            }

            public IVirtualChangeSet<TObject, TKey> Virtualise(IVirtualRequest parameters)
            {
                if (parameters == null || parameters.StartIndex < 0 || parameters.Size < 1)
                {
                    return null;
                }
                if (parameters.Size == _parameters.Size && parameters.StartIndex == _parameters.StartIndex)
                    return null;

                _parameters = parameters;
                return Virtualise();
            }

            public IVirtualChangeSet<TObject, TKey> Update(ISortedChangeSet<TObject, TKey> updates)
            {
                _isLoaded = true;
                _all = updates.SortedItems;
                return Virtualise(updates);
            }

            private IVirtualChangeSet<TObject, TKey> Virtualise(ISortedChangeSet<TObject, TKey> updates = null)
            {
                if (_isLoaded == false) return null;

                var previous = _current;
                var virualised = _all.Skip(_parameters.StartIndex)
                                     .Take(_parameters.Size)
                                     .ToList();

                _current = new KeyValueCollection<TObject, TKey>(virualised, _all.Comparer, updates?.SortedItems.SortReason ?? SortReason.DataChanged, _all.Optimisations);

                ////check for changes within the current virtualised page.  Notify if there have been changes or if the overall count has changed
                var notifications = _changedCalculator.Calculate(_current, previous, updates);
                if (notifications.Count == 0 && (previous.Count != _current.Count))
                {
                    return null;
                }

                var response = new VirtualResponse(_parameters.Size, _parameters.StartIndex, _all.Count);
                return new VirtualChangeSet<TObject, TKey>(notifications, _current, response);
            }
        }
    }
}
