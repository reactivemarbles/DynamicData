using System;
using System.Linq;
using ReactiveUI;

namespace DynamicData.ReactiveUI
{
    /// <summary>
    /// Adaptor used to populate a <see cref="ReactiveList{TObject}"/> from an observable sortedchangeset.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    internal class SortedReactiveListAdaptor<TObject, TKey> : ISortedChangeSetAdaptor<TObject, TKey>
    {
        private readonly ReactiveList<TObject> _target;
        private readonly int _resetThreshold;

        /// <summary>
        /// Initializes a new instance of the <see cref="SortedReactiveListAdaptor{TObject, TKey}"/> class.
        /// </summary>
        /// <param name="target">The target.</param>
        /// <param name="resetThreshold">The reset threshold.</param>
        /// <exception cref="System.ArgumentNullException">target</exception>
        public SortedReactiveListAdaptor(ReactiveList<TObject> target, int resetThreshold = 50)
        {
            _target = target ?? throw new ArgumentNullException(nameof(target));
            _resetThreshold = resetThreshold;
        }

        /// <summary>
        /// Adapts the specified sorted changeset
        /// </summary>
        /// <param name="changes">The changes.</param>
        public void Adapt(ISortedChangeSet<TObject, TKey> changes)
        {

            switch (changes.SortedItems.SortReason)
            {
                case SortReason.InitialLoad:
                case SortReason.ComparerChanged:
                case SortReason.Reset:
                {
                    using (_target.SuppressChangeNotifications())
                    {
                        _target.Clear();
                        _target.AddRange(changes.SortedItems.Select(kv => kv.Value));
                    }
                }
                    break;

                case SortReason.DataChanged:
                {
                    if (changes.Count > _resetThreshold)
                    {
                        using (_target.SuppressChangeNotifications())
                        {
                            _target.Clear();
                            _target.AddRange(changes.SortedItems.Select(kv => kv.Value));
                        }
                    }
                    else
                    {
                        DoUpdate(changes);
                    }
                }
                    break;
                case SortReason.Reorder:

                    //Updates will only be moves, so appply logic
                    DoUpdate(changes);
                    break;


                default:
                    throw new ArgumentOutOfRangeException();
            }
        }


        private void DoUpdate(IChangeSet<TObject, TKey> changes)
        {
            foreach (var change in changes)
            {
                switch (change.Reason)
                {
                    case ChangeReason.Add:
                        _target.Insert(change.CurrentIndex, change.Current);
                        break;
                    case ChangeReason.Remove:
                        _target.RemoveAt(change.CurrentIndex);
                        break;
                    case ChangeReason.Moved:
                        _target.Move(change.PreviousIndex, change.CurrentIndex);
                        break;
                    case ChangeReason.Update:
                        {
                            _target.RemoveAt(change.PreviousIndex);
                            _target.Insert(change.CurrentIndex, change.Current);
                        }
                        break;
                }
            }

        }
    }
}