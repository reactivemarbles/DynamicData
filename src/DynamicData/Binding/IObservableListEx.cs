using System;
using System.Linq;
using System.Reactive.Linq;

namespace DynamicData.Binding
{
    /// <summary>
    /// Extensions to convert a dynamic stream out to an <see cref="IObservableList{T}"/>.
    /// </summary>
    public static class IObservableListEx
    {
        /// <summary>
        /// Binds the results to the specified <see cref="IObservableList{T}"/>. Unlike
        /// binding to a <see cref="ReadOnlyObservableCollection{T}"/> which loses the
        /// ability to refresh items, binding to an <see cref="IObservableList{T}"/>.
        /// allows for refresh changes to be preserved and keeps the list read-only.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="destination">The destination.</param>
        /// <returns>The <paramref name="source"/> changeset for continued chaining.</returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservable<IChangeSet<TObject>> BindToObservableList<TObject>(
            this IObservable<IChangeSet<TObject>> source,
            out IObservableList<TObject> observableList)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            // Load our source list with the change set.
            // Each changeset we need to convert to remove the key.
            var sourceList = new SourceList<TObject>(source);

            // Output our readonly observable list, preventing the sourcelist from being editted from anywhere else.
            observableList = sourceList;

            // Return a observable that will connect to the source so we can properly dispose when the pipeline ends.
            return Observable.Create<IChangeSet<TObject>>(observer =>
            {
                return source
                    .Finally(() => sourceList.Dispose())
                    .SubscribeSafe(observer);
            });
        }

        /// <summary>
        /// Binds the results to the specified <see cref="IObservableList{T}"/>. Unlike
        /// binding to a <see cref="ReadOnlyObservableCollection{T}"/> which loses the
        /// ability to refresh items, binding to an <see cref="IObservableList{T}"/>.
        /// allows for refresh changes to be preserved and keeps the list read-only.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="destination">The destination.</param>
        /// <returns>The <paramref name="source"/> changeset for continued chaining.</returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservable<IChangeSet<TObject, TKey>> BindToObservableList<TObject, TKey>(
            this IObservable<IChangeSet<TObject, TKey>> source,
            out IObservableList<TObject> observableList)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            // Load our source list with the change set.
            // Each changeset we need to convert to remove the key.
            var sourceList = new SourceList<TObject>();

            // Output our readonly observable list, preventing the sourcelist from being editted from anywhere else.
            observableList = sourceList;

            // Return a observable that will connect to the source so we can properly dispose when the pipeline ends.
            return Observable.Create<IChangeSet<TObject, TKey>>(observer =>
            {
                return source
                    .Do(changes => sourceList.Edit(editor => editor.Clone(changes.Convert(editor))))
                    .Finally(() => sourceList.Dispose())
                    .SubscribeSafe(observer);
            });
        }

        /// <summary>
        /// Binds the results to the specified <see cref="IObservableList{T}"/>. Unlike
        /// binding to a <see cref="ReadOnlyObservableCollection{T}"/> which loses the
        /// ability to refresh items, binding to an <see cref="IObservableList{T}"/>.
        /// allows for refresh changes to be preserved and keeps the list read-only.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="destination">The destination.</param>
        /// <returns>The <paramref name="source"/> changeset for continued chaining.</returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservable<ISortedChangeSet<TObject, TKey>> BindToObservableList<TObject, TKey>(
            this IObservable<ISortedChangeSet<TObject, TKey>> source,
            out IObservableList<TObject> observableList)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            // Load our source list with the change set.
            // Each changeset we need to convert to remove the key.
            var sourceList = new SourceList<TObject>();

            // Output our readonly observable list, preventing the sourcelist from being editted from anywhere else.
            observableList = sourceList;

            // Return a observable that will connect to the source so we can properly dispose when the pipeline ends.
            return Observable.Create<ISortedChangeSet<TObject, TKey>>(observer =>
            {
                return source
                    .Do(changes => sourceList.Edit(editor => editor.Clone(changes.Convert(editor))))
                    .Finally(() => sourceList.Dispose())
                    .SubscribeSafe(observer);
            });
        }

        /// <summary>
        /// Converts a <see cref="IChangeSet{TObject, TKey}"/> to <see cref="IChangeSet{TObject}"/>
        /// which allows for binding a cache to a list.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="changeSetWithKey">The source change set</param>
        /// <param name="list">
        /// An optional list, if provided it allows the refresh from a key based cache to find the index for the resulting list based refresh.
        /// If not provided a refresh will dropdown to a replace which may ultimately result in a remove+add change downstream.
        /// </param>
        /// <returns>The downcasted <see cref="IChangeSet{TObject}"/></returns>
        private static IChangeSet<TObject> Convert<TObject, TKey>(this IChangeSet<TObject, TKey> changeSetWithKey, IExtendedList<TObject> list = null)
        {
            return new ChangeSet<TObject>(changeSetWithKey.Select(change => Convert(change, list)));
        }

        /// <summary>
        /// Converts a <see cref="Change{TObject, TKey}"/> to <see cref="ChangeSet{TObject}"/>
        /// which allows for binding a cache to a list.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="change">The source change</param>
        /// <param name="list">
        /// An optional list, if provided it allows the refresh from a key based cache to find the index for the resulting list based refresh.
        /// If not provided a refresh will dropdown to a replace which may ultimately result in a remove+add change downstream.
        /// </param>
        /// <returns>The downcasted <see cref="Change{TObject}"/></returns>
        private static Change<TObject> Convert<TObject, TKey>(Change<TObject, TKey> change, IExtendedList<TObject> list = null)
        {
            switch (change.Reason)
            {
                case ChangeReason.Add:
                    // If not sorted
                    if (change.CurrentIndex == -1)
                    {
                        return new Change<TObject>(ListChangeReason.Add, change.Current);
                    }

                    return new Change<TObject>(ListChangeReason.Add, change.Current, index: change.CurrentIndex);

                case ChangeReason.Moved:
                    // Move is always sorted
                    return new Change<TObject>(change.Current, change.CurrentIndex, change.PreviousIndex);
                case ChangeReason.Refresh:
                    // Refresh needs an index, which we don't have in a Change<T, K> model since it's key based.
                    // See: DynamicData > Binding > ObservableCollectionAdaptor.cs Line 129-130

                    // Note: A refresh is not index based within the context of a sorted change.
                    // Thus, currentIndex will not be available here where as other changes like add and remove do have indexes if coming from a sorted changeset.

                    // In order to properly handle a refresh and map to an index on a list, we need to use the source list (within the edit method so that it's thread safe)
                    if (list != null && list.IndexOf(change.Current) is int index && index >= 0)
                    {
                        return new Change<TObject>(ListChangeReason.Refresh, current: change.Current, index: index);
                    }

                    // Fallback to a replace if a list is not available
                    return new Change<TObject>(ListChangeReason.Replace, current: change.Current, previous: change.Current);
                case ChangeReason.Remove:
                    // If not sorted
                    if (change.CurrentIndex == -1)
                    {
                        return new Change<TObject>(ListChangeReason.Remove, change.Current);
                    }
                    return new Change<TObject>(ListChangeReason.Remove, change.Current, index: change.CurrentIndex);

                case ChangeReason.Update:
                    // If not sorted
                    if (change.CurrentIndex == -1)
                    {
                        return new Change<TObject>(ListChangeReason.Replace, change.Current, previous: change.Previous.Value);
                    }

                    return new Change<TObject>(ListChangeReason.Replace, change.Current, previous: change.Previous, currentIndex: change.CurrentIndex, previousIndex: change.PreviousIndex);
                default:
                    throw new ArgumentOutOfRangeException(nameof(change.Reason), change.Reason, $"Please add {change.Reason} to the switch statement.");
            }

        }
    }
}