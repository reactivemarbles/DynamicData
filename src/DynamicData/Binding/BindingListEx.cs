// Copyright (c) 2011-2019 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;

namespace DynamicData.Binding
{
    /// <summary>
    /// Extensions to convert an binding list into a dynamic stream
    /// </summary>
    public static class BindingListEx
    {
        /// <summary>
        /// Convert a binding list into an observable change set
        /// </summary>
        /// <typeparam name="T">The type of the object.</typeparam>
        /// <param name="source">The source.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservable<IChangeSet<T>> ToObservableChangeSet<T>(this BindingList<T> source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            return ToObservableChangeSet<BindingList<T>, T>(source);
        }

        /// <summary>
        /// Convert a binding list into an observable change set
        /// </summary>
        /// <typeparam name="TObject">The type of the object.</typeparam>
        /// <typeparam name="TKey">The type of the key.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="keySelector">The key selector.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source
        /// or
        /// keySelector</exception>
        public static IObservable<IChangeSet<TObject, TKey>> ToObservableChangeSet<TObject, TKey>(this BindingList<TObject> source, Func<TObject, TKey> keySelector)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (keySelector == null)
            {
                throw new ArgumentNullException(nameof(keySelector));
            }

            return ToObservableChangeSet<BindingList<TObject>, TObject>(source).AddKey(keySelector);
        }

        /// <summary>
        /// Convert a binding list into an observable change set
        /// </summary>
        /// <typeparam name="T">The type of the object.</typeparam>
        /// <typeparam name="TCollection"></typeparam>
        /// <param name="source">The source.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">source</exception>
        public static IObservable<IChangeSet<T>> ToObservableChangeSet<TCollection, T>(this TCollection source)
            where TCollection : IBindingList, IEnumerable<T>
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            return Observable.Create<IChangeSet<T>>(observer =>
            {
                var data = new ChangeAwareList<T>(source);

                if (data.Count > 0)
                {
                    observer.OnNext(data.CaptureChanges());
                }

                return source.ObserveCollectionChanges()
                    .Scan(data, (list, args) =>
                    {
                        var changes = args.EventArgs;

                        switch (changes.ListChangedType)
                        {
                            case ListChangedType.ItemAdded:
                                {
                                    list.Add((T)source[changes.NewIndex]);
                                    break;
                                }

                            case ListChangedType.ItemDeleted:
                                {
                                    list.RemoveAt(changes.NewIndex);
                                    break;
                                }

                            case ListChangedType.ItemChanged:
                                {
                                    list[changes.NewIndex] = (T)source[changes.NewIndex];
                                    break;
                                }

                            case ListChangedType.Reset:
                                {
                                    list.Clear();
                                    list.AddRange(source);
                                    break;
                                }
                        }

                        return list;
                    })
                    .Select(list => list.CaptureChanges())
                    .SubscribeSafe(observer);
            });
        }

        /// <summary>
        /// Observes list changed args
        /// </summary>
        public static IObservable<EventPattern<ListChangedEventArgs>> ObserveCollectionChanges(this IBindingList source)
        {
            return Observable
                .FromEventPattern<ListChangedEventHandler, ListChangedEventArgs>(
                    h => source.ListChanged += h,
                    h => source.ListChanged -= h);
        }
    }
}
