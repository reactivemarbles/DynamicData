// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Reactive;
using System.Reactive.Linq;
using DynamicData.Kernel;

namespace DynamicData.Binding;

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
    public static IObservable<EventPattern<NotifyCollectionChangedEventArgs>> ObserveCollectionChanges(this INotifyCollectionChanged source) => Observable.FromEventPattern<NotifyCollectionChangedEventHandler, NotifyCollectionChangedEventArgs>(h => source.CollectionChanged += h, h => source.CollectionChanged -= h);

    /// <summary>
    /// Convert an observable collection into an observable change set.
    /// Change set observes collection change events.
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="source">The source.</param>
    /// <returns>An observable that emits the change set.</returns>
    /// <exception cref="ArgumentNullException">source.</exception>
    public static IObservable<IChangeSet<T>> ToObservableChangeSet<T>(this ObservableCollection<T> source)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

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
    /// <exception cref="ArgumentNullException">source
    /// or
    /// keySelector.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> ToObservableChangeSet<TObject, TKey>(this ObservableCollection<TObject> source, Func<TObject, TKey> keySelector)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        keySelector.ThrowArgumentNullExceptionIfNull(nameof(keySelector));

        return ToObservableChangeSet<ObservableCollection<TObject>, TObject>(source).AddKey(keySelector);
    }

    /// <summary>
    /// Convert the readonly observable collection into an observable change set.
    /// Change set observes collection change events.
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="source">The source.</param>
    /// <returns>An observable that emits the change set.</returns>
    /// <exception cref="ArgumentNullException">source.</exception>
    public static IObservable<IChangeSet<T>> ToObservableChangeSet<T>(this ReadOnlyObservableCollection<T> source)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

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
    /// <exception cref="ArgumentNullException">source
    /// or
    /// keySelector.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> ToObservableChangeSet<TObject, TKey>(this ReadOnlyObservableCollection<TObject> source, Func<TObject, TKey> keySelector)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        keySelector.ThrowArgumentNullExceptionIfNull(nameof(keySelector));

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
    /// <exception cref="ArgumentNullException">source.</exception>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Roslynator", "RCS1146:Use conditional access.", Justification = "net 7.0 has error when conditional access is used.")]
    public static IObservable<IChangeSet<T>> ToObservableChangeSet<TCollection, T>(this TCollection source)
        where TCollection : INotifyCollectionChanged, IEnumerable<T>
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return Observable.Create<IChangeSet<T>>(
            observer =>
            {
                var data = new ChangeAwareList<T>(source);

                observer.OnNext(data.CaptureChanges());

                return source.ObserveCollectionChanges().Scan(
                    data,
                    (list, args) =>
                    {
                        var changes = args.EventArgs;

                        switch (changes.Action)
                        {
                            case NotifyCollectionChangedAction.Add when changes.NewItems is not null:
                                {
                                    var newIndex = changes.NewStartingIndex == -1
                                        ? list.Count
                                        : changes.NewStartingIndex;

                                    if (changes.NewItems.Count == 1 && changes.NewItems[0] is T item)
                                    {
                                        list.Insert(newIndex, item);
                                    }
                                    else
                                    {
                                        list.InsertRange(changes.NewItems.Cast<T>(), newIndex);
                                    }

                                    break;
                                }

                            case NotifyCollectionChangedAction.Remove when changes.OldItems is not null:
                                {
                                    if (changes.OldStartingIndex == -1)
                                    {
                                        foreach (var item in changes.OldItems.Cast<T>())
                                        {
                                            list.Remove(item);
                                        }
                                    }
                                    else if (changes.OldItems.Count == 1)
                                    {
                                        list.RemoveAt(changes.OldStartingIndex);
                                    }
                                    else
                                    {
                                        list.RemoveRange(changes.OldStartingIndex, changes.OldItems.Count);
                                    }

                                    break;
                                }

                            case NotifyCollectionChangedAction.Replace when changes.NewItems is not null &&
                                                                            changes.NewItems[0] is T replacedItem:
                                {
                                    if (changes.NewStartingIndex == -1)
                                    {
                                        var original = changes.OldItems!.Cast<T>().SingleOrDefault();
                                        var oldIndex = list.IndexOf(original!);

                                        list[oldIndex] = replacedItem;
                                    }
                                    else
                                    {
                                        list[changes.NewStartingIndex] = replacedItem;
                                    }
                                }

                                break;
                            case NotifyCollectionChangedAction.Reset:
                                list.Clear();
                                list.AddRange(source);
                                break;
                            case NotifyCollectionChangedAction.Move:

                                if (changes.OldStartingIndex == -1)
                                {
                                    throw new UnspecifiedIndexException("Move -> OldStartingIndex");
                                }

                                if (changes.NewStartingIndex == -1)
                                {
                                    throw new UnspecifiedIndexException("Move -> NewStartingIndex");
                                }

                                list.Move(changes.OldStartingIndex, changes.NewStartingIndex);
                                break;
                        }

                        return list;
                    }).Select(list => list.CaptureChanges()).SubscribeSafe(observer);
            });
    }
}
