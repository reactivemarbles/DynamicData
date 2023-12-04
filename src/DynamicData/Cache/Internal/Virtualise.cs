// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Linq;

namespace DynamicData.Cache.Internal;

internal sealed class Virtualise<TObject, TKey>(IObservable<ISortedChangeSet<TObject, TKey>> source, IObservable<IVirtualRequest> virtualRequests)
    where TObject : notnull
    where TKey : notnull
{
    private readonly IObservable<ISortedChangeSet<TObject, TKey>> _source = source ?? throw new ArgumentNullException(nameof(source));

    private readonly IObservable<IVirtualRequest> _virtualRequests = virtualRequests ?? throw new ArgumentNullException(nameof(virtualRequests));

    public IObservable<IVirtualChangeSet<TObject, TKey>> Run() => Observable.Create<IVirtualChangeSet<TObject, TKey>>(
            observer =>
            {
                var virtualiser = new Virtualiser();
                var locker = new object();

                var request = _virtualRequests.Synchronize(locker).Select(virtualiser.Virtualise).Where(x => x is not null).Select(x => x!);
                var dataChange = _source.Synchronize(locker).Select(virtualiser.Update).Where(x => x is not null).Select(x => x!);
                return request.Merge(dataChange).Where(updates => updates is not null).SubscribeSafe(observer);
            });

    private sealed class Virtualiser(VirtualRequest? request = null)
    {
        private IKeyValueCollection<TObject, TKey> _all = new KeyValueCollection<TObject, TKey>();

        private IKeyValueCollection<TObject, TKey> _current = new KeyValueCollection<TObject, TKey>();

        private bool _isLoaded;

        private IVirtualRequest _parameters = request ?? new VirtualRequest();

        public IVirtualChangeSet<TObject, TKey>? Update(ISortedChangeSet<TObject, TKey> updates)
        {
            _isLoaded = true;
            _all = updates.SortedItems;
            return Virtualise(updates);
        }

        public IVirtualChangeSet<TObject, TKey>? Virtualise(IVirtualRequest? parameters)
        {
            if (parameters is null || parameters.StartIndex < 0 || parameters.Size < 1)
            {
                return null;
            }

            if (parameters.Size == _parameters.Size && parameters.StartIndex == _parameters.StartIndex)
            {
                return null;
            }

            _parameters = parameters;
            return Virtualise();
        }

        private VirtualChangeSet<TObject, TKey>? Virtualise(ISortedChangeSet<TObject, TKey>? updates = null)
        {
            if (!_isLoaded)
            {
                return null;
            }

            var previous = _current;
            var virtualised = _all.Skip(_parameters.StartIndex).Take(_parameters.Size).ToList();

            _current = new KeyValueCollection<TObject, TKey>(virtualised, _all.Comparer, updates?.SortedItems.SortReason ?? SortReason.DataChanged, _all.Optimisations);

            // check for changes within the current virtualised page.  Notify if there have been changes or if the overall count has changed
            var notifications = FilteredIndexCalculator<TObject, TKey>.Calculate(_current, previous, updates);
            if (notifications.Count == 0 && (previous.Count != _current.Count))
            {
                return null;
            }

            var response = new VirtualResponse(_parameters.Size, _parameters.StartIndex, _all.Count);
            return new VirtualChangeSet<TObject, TKey>(notifications, _current, response);
        }
    }
}
