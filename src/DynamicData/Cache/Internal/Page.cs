// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Cache.Internal;
#else

namespace DynamicData.Cache.Internal;
#endif

/// <summary>
/// Provides members for the Page class.
/// </summary>
/// <typeparam name="TObject">The type of the TObject value.</typeparam>
/// <typeparam name="TKey">The type of the TKey value.</typeparam>
/// <param name="source">The source value.</param>
/// <param name="pageRequests">The pageRequests value.</param>
internal sealed class Page<TObject, TKey>(IObservable<ISortedChangeSet<TObject, TKey>> source, IObservable<IPageRequest> pageRequests)
    where TObject : notnull
    where TKey : notnull
{
    /// <summary>
    /// Executes the Run operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public IObservable<IPagedChangeSet<TObject, TKey>> Run() => Observable.Create<IPagedChangeSet<TObject, TKey>>(
            observer =>
            {
                var queue = new SharedDeliveryQueue();
                var paginator = new Paginator();
                var request = pageRequests.SynchronizeSafe(queue).Select(paginator.Paginate);
                var dataChange = source.SynchronizeSafe(queue).Select(paginator.Update);

                return new CompositeDisposable(request.Merge(dataChange)
                    .Where(updates => updates is not null)
                    .Select(x => x!)
                    .SubscribeSafe(observer), queue);
            });

/// <summary>
/// Provides members for the Paginator class.
/// </summary>
private sealed class Paginator
    {
        /// <summary>
        /// The _all field.
        /// </summary>
        private IKeyValueCollection<TObject, TKey> _all = new KeyValueCollection<TObject, TKey>();

        /// <summary>
        /// The _current field.
        /// </summary>
        private KeyValueCollection<TObject, TKey> _current = new();

        /// <summary>
        /// The _isLoaded field.
        /// </summary>
        private bool _isLoaded;

        /// <summary>
        /// The _request field.
        /// </summary>
        private IPageRequest _request;

        /// <summary>
        /// Initializes a new instance of the <see cref="Paginator"/> class.
        /// </summary>
        public Paginator()
        {
            _request = PageRequest.Default;
            _isLoaded = false;
        }

        /// <summary>
        /// Executes the Paginate operation.
        /// </summary>
        /// <param name="parameters">The parameters value.</param>
        /// <returns>The result of the operation.</returns>
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

        /// <summary>
        /// Executes the Update operation.
        /// </summary>
        /// <param name="updates">The updates value.</param>
        /// <returns>The result of the operation.</returns>
        public IPagedChangeSet<TObject, TKey>? Update(ISortedChangeSet<TObject, TKey> updates)
        {
            _isLoaded = true;
            _all = updates.SortedItems;
            return Paginate(updates);
        }

        /// <summary>
        /// Executes the CalculatePages operation.
        /// </summary>
        /// <returns>The result of the operation.</returns>
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

        /// <summary>
        /// Executes the Paginate operation.
        /// </summary>
        /// <param name="updates">The updates value.</param>
        /// <returns>The result of the operation.</returns>
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
