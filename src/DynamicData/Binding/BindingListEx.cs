// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reactive;
using System.Reactive.Linq;

namespace DynamicData.Binding;

/// <summary>
/// Extensions to convert an binding list into a dynamic stream.
/// </summary>
public static class BindingListEx
{
    /// <summary>
    /// Observes list changed args.
    /// </summary>
    /// <param name="source">The source list.</param>
    /// <returns>An observable which emits event pattern changed event args.</returns>
    public static IObservable<EventPattern<ListChangedEventArgs>> ObserveCollectionChanges(this IBindingList source) =>
        Observable.FromEventPattern<ListChangedEventHandler, ListChangedEventArgs>(h => source.ListChanged += h, h => source.ListChanged -= h);

    /// <summary>
    /// Convert a binding list into an observable change set.
    /// Change set observes list change events.
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="source">The source.</param>
    /// <returns>An observable which emits change set values.</returns>
    /// <exception cref="ArgumentNullException">source.</exception>
    public static IObservable<IChangeSet<T>> ToObservableChangeSet<T>(this BindingList<T> source)
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return ToObservableChangeSet<BindingList<T>, T>(source);
    }

    /// <summary>
    /// Convert a binding list into an observable change set.
    /// Change set observes list change events.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source.</param>
    /// <param name="keySelector">The key selector.</param>
    /// <returns>An observable which emits change set values.</returns>
    /// <exception cref="ArgumentNullException">source
    /// or
    /// keySelector.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> ToObservableChangeSet<TObject, TKey>(this BindingList<TObject> source, Func<TObject, TKey> keySelector)
        where TObject : notnull
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        keySelector.ThrowArgumentNullExceptionIfNull(nameof(keySelector));

        return ToObservableChangeSet<BindingList<TObject>, TObject>(source).AddKey(keySelector);
    }

    /// <summary>
    /// Convert a binding list into an observable change set.
    /// Change set observes list change events.
    /// </summary>
    /// <typeparam name="TCollection">The collection type.</typeparam>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="source">The source.</param>
    /// <returns>An observable which emits change set values.</returns>
    /// <exception cref="ArgumentNullException">source.</exception>
    public static IObservable<IChangeSet<T>> ToObservableChangeSet<TCollection, T>(this TCollection source)
        where TCollection : IBindingList, IEnumerable<T>
        where T : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

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

                        switch (changes.ListChangedType)
                        {
                            case ListChangedType.ItemAdded when source[changes.NewIndex] is T newItem:
                                {
                                    if (changes.NewIndex == -1)
                                    {
                                        list.Add(newItem);
                                    }
                                    else
                                    {
                                        list.Insert(changes.NewIndex, newItem);
                                    }

                                    break;
                                }

                            case ListChangedType.ItemDeleted:
                                {
                                    list.RemoveAt(changes.NewIndex);
                                    break;
                                }

                            case ListChangedType.ItemChanged when source[changes.NewIndex] is T newItem:
                                {
                                    list[changes.NewIndex] = newItem;
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
                    }).Select(list => list.CaptureChanges()).SubscribeSafe(observer);
            });
    }

    internal static void Clone<T>(this BindingList<T> source, IEnumerable<Change<T>> changes)
        where T : notnull
    {
        // ** Copied from ListEx for binding list specific changes
        source.ThrowArgumentNullExceptionIfNull(nameof(source));
        changes.ThrowArgumentNullExceptionIfNull(nameof(changes));

        foreach (var item in changes)
        {
            source.Clone(item, EqualityComparer<T>.Default);
        }
    }

    private static void Clone<T>(this BindingList<T> source, Change<T> item, IEqualityComparer<T> equalityComparer)
        where T : notnull
    {
        switch (item.Reason)
        {
            case ListChangeReason.Add:
                {
                    var change = item.Item;
                    var hasIndex = change.CurrentIndex >= 0;
                    if (hasIndex)
                    {
                        source.Insert(change.CurrentIndex, change.Current);
                    }
                    else
                    {
                        source.Add(change.Current);
                    }

                    break;
                }

            case ListChangeReason.AddRange:
                {
                    source.AddOrInsertRange(item.Range, item.Range.Index);
                    break;
                }

            case ListChangeReason.Clear:
                {
                    source.ClearOrRemoveMany(item);
                    break;
                }

            case ListChangeReason.Replace:
                {
                    var change = item.Item;
                    if (change.CurrentIndex >= 0 && change.CurrentIndex == change.PreviousIndex)
                    {
                        source[change.CurrentIndex] = change.Current;
                    }
                    else
                    {
                        if (change.PreviousIndex == -1)
                        {
                            source.Remove(change.Previous.Value);
                        }
                        else
                        {
                            // is this best? or replace + move?
                            source.RemoveAt(change.PreviousIndex);
                        }

                        if (change.CurrentIndex == -1)
                        {
                            source.Add(change.Current);
                        }
                        else
                        {
                            source.Insert(change.CurrentIndex, change.Current);
                        }
                    }

                    break;
                }

            case ListChangeReason.Refresh:
                {
                    var index = source.IndexOf(item.Item.Current);
                    if (index != -1)
                    {
                        source.ResetItem(index);
                    }

                    break;
                }

            case ListChangeReason.Remove:
                {
                    var change = item.Item;
                    var hasIndex = change.CurrentIndex >= 0;
                    if (hasIndex)
                    {
                        source.RemoveAt(change.CurrentIndex);
                    }
                    else
                    {
                        var index = source.IndexOf(change.Current, equalityComparer);
                        if (index > -1)
                        {
                            source.RemoveAt(index);
                        }
                    }

                    break;
                }

            case ListChangeReason.RemoveRange:
                {
                    source.RemoveMany(item.Range);
                    break;
                }

            case ListChangeReason.Moved:
                {
                    var change = item.Item;
                    var hasIndex = change.CurrentIndex >= 0;
                    if (!hasIndex)
                    {
                        throw new UnspecifiedIndexException("Cannot move as an index was not specified");
                    }

                    if (source is IExtendedList<T> extendedList)
                    {
                        extendedList.Move(change.PreviousIndex, change.CurrentIndex);
                    }
                    else if (source is ObservableCollection<T> observableCollection)
                    {
                        observableCollection.Move(change.PreviousIndex, change.CurrentIndex);
                    }
                    else
                    {
                        // check this works whatever the index is
                        source.RemoveAt(change.PreviousIndex);
                        source.Insert(change.CurrentIndex, change.Current);
                    }

                    break;
                }
        }
    }
}
