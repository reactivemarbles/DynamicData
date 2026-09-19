// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Cache.Internal;
#else

namespace DynamicData.Cache.Internal;
#endif

/// <summary>
/// Provides members for the GroupOnObservable class.
/// </summary>
/// <typeparam name="TObject">The type of the TObject value.</typeparam>
/// <typeparam name="TKey">The type of the TKey value.</typeparam>
/// <typeparam name="TGroupKey">The type of the TGroupKey value.</typeparam>
/// <param name="source">The source value.</param>
/// <param name="selectGroup">The selectGroup value.</param>
internal sealed class GroupOnObservable<TObject, TKey, TGroupKey>(IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IObservable<TGroupKey>> selectGroup)
    where TObject : notnull
    where TKey : notnull
    where TGroupKey : notnull
{
    /// <summary>
    /// Executes the Run operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public IObservable<IGroupChangeSet<TObject, TKey, TGroupKey>> Run() =>
        Observable.Create<IGroupChangeSet<TObject, TKey, TGroupKey>>(observer => new Subscription(source, selectGroup, observer));
    // Maintains state for a single subscription

/// <summary>
/// Provides members for the Subscription class.
/// </summary>
private sealed class Subscription : CacheParentSubscription<TObject, TKey, (TGroupKey, TObject), IGroupChangeSet<TObject, TKey, TGroupKey>>
    {
        /// <summary>
        /// The _grouper field.
        /// </summary>
        private readonly DynamicGrouper<TObject, TKey, TGroupKey> _grouper = new();

        /// <summary>
        /// The _selectGroup field.
        /// </summary>
        private readonly Func<TObject, TKey, IObservable<TGroupKey>> _selectGroup;

        /// <summary>
        /// Initializes a new instance of the <see cref="Subscription"/> class.
        /// </summary>
        /// <param name="source">The source value.</param>
        /// <param name="selectGroup">The selectGroup value.</param>
        /// <param name="observer">The observer value.</param>
        public Subscription(IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IObservable<TGroupKey>> selectGroup, IObserver<IGroupChangeSet<TObject, TKey, TGroupKey>> observer)
            : base(observer)
        {
            _selectGroup = selectGroup;
            CreateParentSubscription(source);
        }

        /// <summary>
        /// Executes the ParentOnNext operation.
        /// </summary>
        /// <param name="changes">The changes value.</param>
        protected override void ParentOnNext(IChangeSet<TObject, TKey> changes)
        {
            // Process all the changes at once to preserve the changeset order
            foreach (var change in changes.ToConcreteType())
            {
                _grouper.ProcessChange(change);

                switch (change.Reason)
                {
                    // Shutdown existing sub (if any) and create a new one that
                    // Will update the group key for the current item
                    case ChangeReason.Add or ChangeReason.Update:
                        AddGroupSubscription(change.Current, change.Key);
                        break;

                    // Shutdown the existing subscription
                    case ChangeReason.Remove:
                        RemoveChildSubscription(change.Key);
                        break;
                }
            }
        }

        /// <summary>
        /// Executes the ChildOnNext operation.
        /// </summary>
        /// <param name="tuple">The tuple value.</param>
        /// <param name="parentKey">The parentKey value.</param>
        protected override void ChildOnNext((TGroupKey, TObject) tuple, TKey parentKey) =>
            _grouper.AddOrUpdate(parentKey, tuple.Item1, tuple.Item2);

        /// <summary>
        /// Executes the EmitChanges operation.
        /// </summary>
        /// <param name="observer">The observer value.</param>
        protected override void EmitChanges(IObserver<IGroupChangeSet<TObject, TKey, TGroupKey>> observer) =>
            _grouper.EmitChanges(observer);

        /// <summary>
        /// Executes the Dispose operation.
        /// </summary>
        /// <param name="disposing">The disposing value.</param>
        protected override void Dispose(bool disposing)
        {
            _grouper.Dispose();
            base.Dispose(disposing);
        }

        /// <summary>
        /// Executes the AddGroupSubscription operation.
        /// </summary>
        /// <param name="obj">The obj value.</param>
        /// <param name="key">The key value.</param>
        private void AddGroupSubscription(TObject obj, TKey key) =>
            AddChildSubscription(MakeChildObservable(_selectGroup(obj, key).DistinctUntilChanged().Select(groupKey => (groupKey, obj))), key);
    }
}
