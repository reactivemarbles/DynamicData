using System;
using ReactiveUI;

namespace DynamicData.ReactiveUI
{
    /// <summary>
    /// Adaptor used to populate a <see cref="ReactiveList{TObject}"/> from an observable changeset.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    internal class ObservableListToReactiveListAdaptor<TObject> : IChangeSetAdaptor<TObject>
    {
        private bool _loaded;
        private readonly ReactiveList<TObject> _target;
        private readonly int _resetThreshold;

        /// <summary>
        /// Initializes a new instance of the <see cref="ObservableCacheToReactiveListAdaptor{TObject,TKey}"/> class.
        /// </summary>
        /// <param name="target">The target.</param>
        /// <param name="resetThreshold">The reset threshold.</param>
        /// <exception cref="System.ArgumentNullException">target</exception>
        public ObservableListToReactiveListAdaptor(ReactiveList<TObject> target, int resetThreshold = 50)
        {
            _target = target ?? throw new ArgumentNullException(nameof(target));
            _resetThreshold = resetThreshold;
        }


        /// <summary>
        /// Maintains the specified collection from the changes
        /// </summary>
        /// <param name="changes">The changes.</param>
        public void Adapt(IChangeSet<TObject> changes)
        {
            if (changes.Count > _resetThreshold || !_loaded)
            {
                using (_target.SuppressChangeNotifications())
                {
                    _target.CloneReactiveList(changes);
                    _loaded = true;
                }
            }
            else
            {
                _target.CloneReactiveList(changes);
            }
        }
    }
}