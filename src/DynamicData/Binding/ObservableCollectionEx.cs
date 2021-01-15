// Copyright (c) 2011-2020 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
#if WINUI3UWP
using DynamicData.Binding.WinUI3UWP;
using Microsoft.UI.Xaml.Interop;
#else
using System.Collections.ObjectModel;
using System.Collections.Specialized;
#endif
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;

namespace DynamicData.Binding
{
    /// <summary>
    /// Extensions to convert an observable collection into a dynamic stream.
    /// </summary>
    public static class ObservableCollectionEx
    {
        /// <summary>
        /// Observes notify collection changed args.
        /// </summary>
        /// <param name="source">The source collection.</param>
        /// <returns>An observable that emits the event patterns.</returns>
        public static IObservable<EventPattern<NotifyCollectionChangedEventArgs>> ObserveCollectionChanges(this INotifyCollectionChanged source)
        {
            return Observable.FromEventPattern<NotifyCollectionChangedEventHandler, NotifyCollectionChangedEventArgs>(h => source.CollectionChanged += h, h => source.CollectionChanged -= h);
        }

        /// <summary>
        /// Convert an observable collection into an observable change set.
        /// Change set observes collection change events.
        /// </summary>
        /// <typeparam name="T">The type of the object.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns>An observable that emits the change set.</returns>
        /// <exception cref="System.ArgumentNullException">source.</exception>
        public static IObservable<IChangeSet<T>> ToObservableChangeSet<T>(this ObservableCollection<T> source)
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            return ToObservableChangeSet<ObservableCollection<T>, T>(source);
        }

        /// <summary>
        /// Convert an observable collection into an observable change set.
        /// Change set observes collection change events.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="keySelector">The key selector.</param>
        /// <returns>An observable that emits the change set.</returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// keySelector.</exception>
        public static IObservable<IChangeSet<TObject, TKey>> ToObservableChangeSet<TObject, TKey>(this ObservableCollection<TObject> source, Func<TObject, TKey> keySelector)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (keySelector is null)
            {
                throw new ArgumentNullException(nameof(keySelector));
            }

            return ToObservableChangeSet<ObservableCollection<TObject>, TObject>(source).AddKey(keySelector);
        }

        /// <summary>
        /// Convert the readonly observable collection into an observable change set.
        /// Change set observes collection change events.
        /// </summary>
        /// <typeparam name="T">The type of the object.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns>An observable that emits the change set.</returns>
        /// <exception cref="System.ArgumentNullException">source.</exception>
        public static IObservable<IChangeSet<T>> ToObservableChangeSet<T>(this ReadOnlyObservableCollection<T> source)
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            return ToObservableChangeSet<ReadOnlyObservableCollection<T>, T>(source);
        }

        /// <summary>
        /// Convert the readonly observable collection into an observable change set.
        /// Change set observes collection change events.
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="keySelector">The key selector.</param>
        /// <returns>An observable that emits the change set.</returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// keySelector.</exception>
        public static IObservable<IChangeSet<TObject, TKey>> ToObservableChangeSet<TObject, TKey>(this ReadOnlyObservableCollection<TObject> source, Func<TObject, TKey> keySelector)
            where TKey : notnull
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (keySelector is null)
            {
                throw new ArgumentNullException(nameof(keySelector));
            }

            return ToObservableChangeSet<ReadOnlyObservableCollection<TObject>, TObject>(source).AddKey(keySelector);
        }

        /// <summary>
        /// Convert an observable collection into an observable change set.
        /// Change set observes collection change events.
        /// </summary>
        /// <typeparam name="TCollection">The type of collection.</typeparam>
        /// <typeparam name="T">The type of the object.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns>An observable that emits the change set.</returns>
        /// <exception cref="System.ArgumentNullException">source.</exception>
        public static IObservable<IChangeSet<T>> ToObservableChangeSet<TCollection, T>(this TCollection source)
            where TCollection : INotifyCollectionChanged, IEnumerable<T>
        {
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            return Observable.Create<IChangeSet<T>>(
                observer =>
                    {
                        var data = new ChangeAwareList<T>(source);

                        if (data.Count > 0)
                        {
                            observer.OnNext(data.CaptureChanges());
                        }

                        return source.ObserveCollectionChanges().Scan(
                            data,
                            (list, args) =>
                                {
                                    var changes = args.EventArgs;

                                    switch (changes.Action)
                                    {
                                        case NotifyCollectionChangedAction.Add when changes.NewItems is not null:
                                            {
#if WINUI3UWP
                                                if (changes.NewItems.Size == 1 && changes.NewItems.GetAt(0) is T item)
#else
                                                if (changes.NewItems.Count == 1 && changes.NewItems[0] is T item)
#endif
                                                {
                                                    list.Insert(changes.NewStartingIndex, item);
                                                }
                                                else
                                                {
#if WINUI3UWP
                                                    list.InsertRange((changes.NewItems as System.Collections.IList)?.Cast<T>(), changes.NewStartingIndex);
#else
                                                    list.InsertRange(changes.NewItems.Cast<T>(), changes.NewStartingIndex);
#endif
                                                }

                                                break;
                                            }

                                        case NotifyCollectionChangedAction.Remove when changes.OldItems is not null:
                                            {
#if WINUI3UWP
                                                if (changes.OldItems.Size == 1)
#else
                                                if (changes.OldItems.Count == 1)
#endif
                                                {
                                                    list.RemoveAt(changes.OldStartingIndex);
                                                }
                                                else
                                                {
#if WINUI3UWP
                                                    list.RemoveRange(changes.OldStartingIndex, (int)changes.OldItems.Size);
#else
                                                    list.RemoveRange(changes.OldStartingIndex, changes.OldItems.Count);
#endif
                                                }

                                                break;
                                            }
#if WINUI3UWP
                                        case NotifyCollectionChangedAction.Replace when changes.NewItems?.GetAt(0) is T replacedItem:
#else
                                        case NotifyCollectionChangedAction.Replace when changes.NewItems?[0] is T replacedItem:
#endif
                                            list[changes.NewStartingIndex] = replacedItem;
                                            break;
                                        case NotifyCollectionChangedAction.Reset:
                                            list.Clear();
                                            list.AddRange(source);
                                            break;
                                        case NotifyCollectionChangedAction.Move:
                                            list.Move(changes.OldStartingIndex, changes.NewStartingIndex);
                                            break;
                                    }

                                    return list;
                                }).Select(list => list.CaptureChanges()).SubscribeSafe(observer);
                    });
        }
    }
}