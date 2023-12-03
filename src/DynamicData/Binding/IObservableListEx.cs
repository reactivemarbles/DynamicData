// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Reactive.Linq;

namespace DynamicData.Binding;

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
    /// <param name="observableList">The output observable list.</param>
    /// <returns>The <paramref name="source"/> change set for continued chaining.</returns>
    /// <exception cref="ArgumentNullException">source.</exception>
    public static IObservable<IChangeSet<TObject>> BindToObservableList<TObject>(this IObservable<IChangeSet<TObject>> source, out IObservableList<TObject> observableList)
        where TObject : notnull
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        // Load our source list with the change set.
        // Each change set we need to convert to remove the key.
        var sourceList = new SourceList<TObject>(source);

        // Output our readonly observable list, preventing the source list from being edited from anywhere else.
        observableList = sourceList;

        // Return a observable that will connect to the source so we can properly dispose when the pipeline ends.
        return Observable.Create<IChangeSet<TObject>>(observer => { return source.Finally(() => sourceList.Dispose()).SubscribeSafe(observer); });
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
    /// <param name="observableList">The observable list which is the output.</param>
    /// <returns>The <paramref name="source"/> change set for continued chaining.</returns>
    /// <exception cref="ArgumentNullException">source.</exception>
    public static IObservable<IChangeSet<TObject, TKey>> BindToObservableList<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, out IObservableList<TObject> observableList)
        where TObject : notnull
        where TKey : notnull
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        // Load our source list with the change set.
        // Each change set we need to convert to remove the key.
        var sourceList = new SourceList<TObject>();

        // Output our readonly observable list, preventing the source list from being edited from anywhere else.
        observableList = sourceList;

        // Return a observable that will connect to the source so we can properly dispose when the pipeline ends.
        return Observable.Create<IChangeSet<TObject, TKey>>(observer => { return source.Do(changes => sourceList.Edit(editor => editor.Clone(changes.RemoveKey(editor)))).Finally(() => sourceList.Dispose()).SubscribeSafe(observer); });
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
    /// <param name="observableList">The output observable list.</param>
    /// <returns>The <paramref name="source"/> change set for continued chaining.</returns>
    /// <exception cref="ArgumentNullException">source.</exception>
    public static IObservable<ISortedChangeSet<TObject, TKey>> BindToObservableList<TObject, TKey>(this IObservable<ISortedChangeSet<TObject, TKey>> source, out IObservableList<TObject> observableList)
        where TObject : notnull
        where TKey : notnull
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        // Load our source list with the change set.
        // Each change set we need to convert to remove the key.
        var sourceList = new SourceList<TObject>();

        // Output our readonly observable list, preventing the source list from being edited from anywhere else.
        observableList = sourceList;

        // Return a observable that will connect to the source so we can properly dispose when the pipeline ends.
        return Observable.Create<ISortedChangeSet<TObject, TKey>>(
            observer =>
            {
                return source.Do(
                    changes =>
                    {
                        switch (changes.SortedItems.SortReason)
                        {
                            case SortReason.InitialLoad:
                                sourceList.AddRange(changes.SortedItems.Select(kv => kv.Value));
                                break;

                            case SortReason.ComparerChanged:
                            case SortReason.DataChanged:
                            case SortReason.Reorder:
                                sourceList.Edit(editor => editor.Clone(changes.RemoveKey(editor)));
                                break;

                            case SortReason.Reset:
                                sourceList.Edit(
                                    editor =>
                                    {
                                        editor.Clear();
                                        editor.AddRange(changes.SortedItems.Select(kv => kv.Value));
                                    });
                                break;
                        }
                    }).Finally(() => sourceList.Dispose()).SubscribeSafe(observer);
            });
    }

    /// <summary>
    /// Converts a <see cref="IChangeSet{TObject, TKey}"/> to <see cref="IChangeSet{TObject}"/>
    /// which allows for binding a cache to a list.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="changeSetWithKey">The source change set.</param>
    /// <param name="list">The list needed to support refresh.</param>
    /// <returns>The down casted <see cref="IChangeSet{TObject}"/>.</returns>
    private static IChangeSet<TObject> RemoveKey<TObject, TKey>(this IChangeSet<TObject, TKey> changeSetWithKey, IExtendedList<TObject> list)
        where TObject : notnull
        where TKey : notnull
    {
        var enumerator = new Cache.Internal.RemoveKeyEnumerator<TObject, TKey>(changeSetWithKey, list);

        return new ChangeSet<TObject>(enumerator);
    }
}
