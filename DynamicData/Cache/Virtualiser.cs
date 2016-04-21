using System.Linq;
using DynamicData.Internal;

namespace DynamicData
{
    internal sealed class Virtualiser<TObject, TKey>
    {
        #region Fields

        private readonly FilteredIndexCalculator<TObject, TKey> _changedCalculator = new FilteredIndexCalculator<TObject, TKey>();
        private IKeyValueCollection<TObject, TKey> _all = new KeyValueCollection<TObject, TKey>();
        private IKeyValueCollection<TObject, TKey> _current = new KeyValueCollection<TObject, TKey>();
        private IVirtualRequest _parameters;
        private bool _isLoaded;

        #endregion

        #region Construction

        public Virtualiser(VirtualRequest request = null)
        {
            _parameters = request ?? new VirtualRequest();
        }

        #endregion

        #region Virtualisation

        /// <summary>
        /// Virtualises using specified parameters.  Returns null if there are no changed
        /// </summary>
        /// <param name="parameters">The parameters.</param>
        /// <returns></returns>
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

        #endregion
    }
}
