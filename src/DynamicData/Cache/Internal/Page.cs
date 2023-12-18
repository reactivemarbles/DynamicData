// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Linq;

namespace DynamicData.Cache.Internal;

internal sealed class Page<TObject, TKey>(IObservable<ISortedChangeSet<TObject, TKey>> source, IObservable<IPageRequest> pageRequests)
    where TObject : notnull
    where TKey : notnull
{
    public IObservable<IPagedChangeSet<TObject, TKey>> Run() => Observable.Create<IPagedChangeSet<TObject, TKey>>(
            observer =>
            {
                var locker = new object();
                var paginator = new Paginator();
                var request = pageRequests.Synchronize(locker).Select(paginator.Paginate);
                var dataChange = source.Synchronize(locker).Select(paginator.Update);

                return request.Merge(dataChange)
                    .Where(updates => updates is not null)
                    .Select(x => x!)
                    .SubscribeSafe(observer);
            });

    private sealed class Paginator
    {
        private IKeyValueCollection<TObject, TKey> _all = new KeyValueCollection<TObject, TKey>();

        private IKeyValueCollection<TObject, TKey> _current = new KeyValueCollection<TObject, TKey>();

        private bool _isLoaded;

        private IPageRequest _request;

        public Paginator()
        {
            _request = PageRequest.Default;
            _isLoaded = false;
        }

        public IPagedChangeSet<TObject, TKey>? Paginate(IPageRequest? parameters)
        {
            if (parameters is null || parameters.Page < 0 || parameters.Size < 1)
            {
                return null;
            }

            if (parameters.Size == _request.Size && parameters.Page == _request.Page)
            {
                return null;
            }

            _request = parameters;

            return Paginate();
        }

        public IPagedChangeSet<TObject, TKey>? Update(ISortedChangeSet<TObject, TKey> updates)
        {
            _isLoaded = true;
            _all = updates.SortedItems;
            return Paginate(updates);
        }

        private int CalculatePages()
        {
            if (_request.Size >= _all.Count)
            {
                return 1;
            }

            var pages = _all.Count / _request.Size;
            var overlap = _all.Count % _request.Size;

            if (overlap == 0)
            {
                return pages;
            }

            return pages + 1;
        }

        private PagedChangeSet<TObject, TKey>? Paginate(ISortedChangeSet<TObject, TKey>? updates = null)
        {
            if (!_isLoaded)
            {
                return null;
            }

            var previous = _current;

            var pages = CalculatePages();
            var page = _request.Page > pages ? pages : _request.Page;
            var skip = _request.Size * (page - 1);

            var paged = _all.Skip(skip).Take(_request.Size).ToList();

            _current = new KeyValueCollection<TObject, TKey>(paged, _all.Comparer, updates?.SortedItems.SortReason ?? SortReason.DataChanged, _all.Optimisations);

            // check for changes within the current virtualised page.  Notify if there have been changes or if the overall count has changed
            var notifications = FilteredIndexCalculator<TObject, TKey>.Calculate(_current, previous, updates);
            if (notifications.Count == 0 && (previous.Count != _current.Count))
            {
                return null;
            }

            var response = new PageResponse(_request.Size, _all.Count, page, pages);

            return new PagedChangeSet<TObject, TKey>(_current, notifications, response);
        }
    }
}
