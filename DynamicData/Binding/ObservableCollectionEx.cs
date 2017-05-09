using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace DynamicData.Binding
{
    /// <summary>
    /// Extensions to convert an observable collection into a dynamic stream
    /// </summary>
    public static class ObservableCollectionEx
    {
        /// <summary>
        /// Convert an observable collection into an observable change set
        /// </summary>
        /// <typeparam name="T">The type of the object.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservable<IChangeSet<T>> ToObservableChangeSet<T>(this ObservableCollection<T> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            return ToObservableChangeSet<ObservableCollection<T>,T>(source);
        }


        /// <summary>
        /// Convert an observable collection into an observable change set
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="keySelector">The key selector.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// keySelector</exception>
        public static IObservable<IChangeSet<TObject, TKey>> ToObservableChangeSet<TObject, TKey>(this ObservableCollection<TObject> source, Func<TObject, TKey> keySelector)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));

            return ToObservableChangeSet<ObservableCollection<TObject>, TObject>(source).AddKey(keySelector);
        }

        /// <summary>
        /// Convert the readonly observable collection into an observable change set
        /// </summary>
        /// <typeparam name="T">The type of the object.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservable<IChangeSet<T>> ToObservableChangeSet<T>(this ReadOnlyObservableCollection<T> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            return ToObservableChangeSet<ReadOnlyObservableCollection<T>, T>(source);
        }

        /// <summary>
        /// Convert the readonly observable collection into an observable change set
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="keySelector">The key selector.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// keySelector</exception>
        public static IObservable<IChangeSet<TObject, TKey>> ToObservableChangeSet<TObject, TKey>(this ReadOnlyObservableCollection<TObject> source, Func<TObject, TKey> keySelector)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (keySelector == null) throw new ArgumentNullException(nameof(keySelector));

            return ToObservableChangeSet<ReadOnlyObservableCollection<TObject>, TObject>(source).AddKey(keySelector);
        }

        /// <summary>
        /// Convert an observable collection into an observable change set
        /// </summary>
        /// <typeparam name="T">The type of the object.</typeparam>
        /// <typeparam name="TCollection"></typeparam>
        /// <param name="source">The source.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservable<IChangeSet<T>> ToObservableChangeSet<TCollection, T>(this TCollection source)
            where TCollection : INotifyCollectionChanged, IEnumerable<T>
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return Observable.Create<IChangeSet<T>>
                (
                    observer =>
                    {
                        var locker = new object();

                        ChangeSet<T> InitialChangeSet()
                        {
                            var initial = new Change<T>(ListChangeReason.AddRange, source.ToList());
                            return new ChangeSet<T>() {initial};
                        }

                        //populate local cache, otherwise there is no way to deal with a reset
                        var cloneOfList = new SourceList<T>();

                        var sourceUpdates = Observable
                            .FromEventPattern<NotifyCollectionChangedEventHandler, NotifyCollectionChangedEventArgs>(
                                h => source.CollectionChanged += h,
                                h => source.CollectionChanged -= h)
                            .Select
                            (
                                args =>
                                {
                                    var changes = args.EventArgs;

                                    switch (changes.Action)
                                    {
                                        case NotifyCollectionChangedAction.Add:
                                            return changes.NewItems.OfType<T>()
                                                          .Select((t, index) => new Change<T>(ListChangeReason.Add, t, index + changes.NewStartingIndex));

                                        case NotifyCollectionChangedAction.Remove:
                                            return changes.OldItems.OfType<T>()
                                                          .Select((t, index) => new Change<T>(ListChangeReason.Remove, t, index + changes.OldStartingIndex));

                                        case NotifyCollectionChangedAction.Replace:
                                            return changes.NewItems.OfType<T>()
                                                          .Select((t, idx) =>
                                                          {
                                                              var old = changes.OldItems[idx];
                                                              return new Change<T>(ListChangeReason.Replace, t, (T)old, idx + changes.NewStartingIndex, + changes.NewStartingIndex);
                                                          });

                                        case NotifyCollectionChangedAction.Reset:
                                            var cleared = new Change<T>(ListChangeReason.Clear, cloneOfList.Items.ToList(), 0);
                                            var clearedChangeSet = new ChangeSet<T>() { cleared };
                                            return clearedChangeSet.Concat(InitialChangeSet());

                                        case NotifyCollectionChangedAction.Move:
                                            var item = changes.NewItems.OfType<T>().First();
                                            var change = new Change<T>(item, changes.NewStartingIndex, changes.OldStartingIndex);
                                            return new[] { change };

                                        default:
                                            return null;
                                    }
                                })
                            .Where(updates => updates != null)
                            .Select(updates => (IChangeSet<T>)new ChangeSet<T>(updates));

                        var cacheLoader = Observable.Defer(()=>Observable.Return(InitialChangeSet()))
                                                .Concat(sourceUpdates)
                                                .PopulateInto(cloneOfList);

                        var subscriber = cloneOfList.Connect().SubscribeSafe(observer);
                        return new CompositeDisposable(cacheLoader, subscriber, cloneOfList);
                    });
        }
    }
}
