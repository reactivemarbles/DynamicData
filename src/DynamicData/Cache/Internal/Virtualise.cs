// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Cache.Internal;
#else

namespace DynamicData.Cache.Internal;
#endif

/// <summary>
/// Provides members for the Virtualise class.
/// </summary>
/// <typeparam name="TObject">The type of the TObject value.</typeparam>
/// <typeparam name="TKey">The type of the TKey value.</typeparam>
/// <param name="source">The source value.</param>
/// <param name="virtualRequests">The virtualRequests value.</param>
internal sealed class Virtualise<TObject, TKey>(IObservable<ISortedChangeSet<TObject, TKey>> source, IObservable<IVirtualRequest> virtualRequests)
    where TObject : notnull
    where TKey : notnull
{
    /// <summary>
    /// The _source field.
    /// </summary>
    private readonly IObservable<ISortedChangeSet<TObject, TKey>> _source = source ?? throw new ArgumentNullException(nameof(source));

    /// <summary>
    /// The _virtualRequests field.
    /// </summary>
    private readonly IObservable<IVirtualRequest> _virtualRequests = virtualRequests ?? throw new ArgumentNullException(nameof(virtualRequests));

    /// <summary>
    /// Executes the Run operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public IObservable<IVirtualChangeSet<TObject, TKey>> Run() => Observable.Create<IVirtualChangeSet<TObject, TKey>>(
            observer =>
            {
                var virtualiser = new Virtualiser();
                var queue = new SharedDeliveryQueue();

                var request = _virtualRequests.SynchronizeSafe(queue).Select(virtualiser.Virtualise).Where(x => x is not null).Select(x => x!);
                var dataChange = _source.SynchronizeSafe(queue).Select(virtualiser.Update).Where(x => x is not null).Select(x => x!);
                return new CompositeDisposable(request.Merge(dataChange).Where(updates => updates is not null).SubscribeSafe(observer), queue);
            });

/// <summary>
/// Provides members for the Virtualiser class.
/// </summary>
/// <param name="request">The request value.</param>
private sealed class Virtualiser(VirtualRequest? request = null)
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
        /// The _parameters field.
        /// </summary>
        private IVirtualRequest _parameters = request ?? new VirtualRequest();

        /// <summary>
        /// Executes the Update operation.
        /// </summary>
        /// <param name="updates">The updates value.</param>
        /// <returns>The result of the operation.</returns>
        public IVirtualChangeSet<TObject, TKey>? Update(ISortedChangeSet<TObject, TKey> updates)
        {
            _isLoaded = true;
            _all = updates.SortedItems;
            return Virtualise(updates);
        }

        /// <summary>
        /// Executes the Virtualise operation.
        /// </summary>
        /// <param name="parameters">The parameters value.</param>
        /// <returns>The result of the operation.</returns>
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

        /// <summary>
        /// Executes the Virtualise operation.
        /// </summary>
        /// <param name="updates">The updates value.</param>
        /// <returns>The result of the operation.</returns>
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
