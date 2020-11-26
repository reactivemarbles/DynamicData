// Copyright (c) 2011-2020 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Linq;
using System.Reactive.Linq;

namespace DynamicData.Cache.Internal
{
    internal class Page<TObject, TKey>
        where TKey : notnull
    {
        private readonly IObservable<IPageRequest> _pageRequests;

        private readonly IObservable<ISortedChangeSet<TObject, TKey>> _source;

        public Page(IObservable<ISortedChangeSet<TObject, TKey>> source, IObservable<IPageRequest> pageRequests)
        {
            _source = source;
            _pageRequests = pageRequests;
        }

        public IObservable<IPagedChangeSet<TObject, TKey>> Run()
        {
            return Observable.Create<IPagedChangeSet<TObject, TKey>>(
                observer =>
                    {
                        var locker = new object();
                        var paginator = new Paginator();
                        var request = _pageRequests.Synchronize(locker).Select(paginator.Paginate);
                        var datachange = _source.Synchronize(locker).Select(paginator.Update);

                        return request.Merge(datachange).Where(updates => updates is not null).Select(x => x!).SubscribeSafe(observer);
                    });
        }

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

                int pages = _all.Count / _request.Size;
                int overlap = _all.Count % _request.Size;

                if (overlap == 0)
                {
                    return pages;
                }

                return pages + 1;
            }

            private IPagedChangeSet<TObject, TKey>? Paginate(ISortedChangeSet<TObject, TKey>? updates = null)
            {
                if (_isLoaded == false)
                {
                    return null;
                }

                if (_request is null)
                {
                    return null;
                }

                var previous = _current;

                int pages = CalculatePages();
                int page = _request.Page > pages ? pages : _request.Page;
                int skip = _request.Size * (page - 1);

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
}