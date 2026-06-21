// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Binding;
#else

namespace DynamicData.Binding;
#endif

/// <summary>
/// Extensions to convert a dynamic stream out to an <c>IObservableList&lt;T&gt;</c>.
/// </summary>
public static class IObservableListEx
{
    /// <summary>
    /// Binds the results to the specified <c>IObservableList&lt;T&gt;</c>. Unlike
    /// binding to a <c>ReadOnlyObservableCollection&lt;T&gt;</c> which loses the
    /// ability to refresh items, binding to an <c>IObservableList&lt;T&gt;</c>.
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
        ArgumentExceptionHelper.ThrowIfNull(source);

        // Load our source list with the change set.
        // Each change set we need to convert to remove the key.
        var sourceList = new SourceList<TObject>(source);

        // Output our readonly observable list, preventing the source list from being edited from anywhere else.
        observableList = sourceList;

        // Return a observable that will connect to the source so we can properly dispose when the pipeline ends.
        return Observable.Create<IChangeSet<TObject>>(observer => source.Finally(() => sourceList.Dispose()).SubscribeSafe(observer));
    }

    /// <summary>
    /// Binds the results to the specified <c>IObservableList&lt;T&gt;</c>. Unlike
    /// binding to a <c>ReadOnlyObservableCollection&lt;T&gt;</c> which loses the
    /// ability to refresh items, binding to an <c>IObservableList&lt;T&gt;</c>.
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
        ArgumentExceptionHelper.ThrowIfNull(source);

        // Load our source list with the change set.
        // Each change set we need to convert to remove the key.
        var sourceList = new SourceList<TObject>();

        // Output our readonly observable list, preventing the source list from being edited from anywhere else.
        observableList = sourceList;

        // Return a observable that will connect to the source so we can properly dispose when the pipeline ends.
        return Observable.Create<IChangeSet<TObject, TKey>>(observer => source.Do(changes => sourceList.Edit(editor => editor.Clone(changes.RemoveKey(editor)))).Finally(() => sourceList.Dispose()).SubscribeSafe(observer));
    }

    /// <summary>
    /// Binds the results to the specified <c>IObservableList&lt;T&gt;</c>. Unlike
    /// binding to a <c>ReadOnlyObservableCollection&lt;T&gt;</c> which loses the
    /// ability to refresh items, binding to an <c>IObservableList&lt;T&gt;</c>.
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
        ArgumentExceptionHelper.ThrowIfNull(source);

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
    /// Converts a <c>IChangeSet&lt;TObject, TKey&gt;</c> to <c>IChangeSet&lt;TObject&gt;</c>
    /// which allows for binding a cache to a list.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="changeSetWithKey">The source change set.</param>
    /// <param name="list">The list needed to support refresh.</param>
    /// <returns>The down casted <c>IChangeSet&lt;TObject&gt;</c>.</returns>
    private static ChangeSet<TObject> RemoveKey<TObject, TKey>(this IChangeSet<TObject, TKey> changeSetWithKey, IExtendedList<TObject> list)
        where TObject : notnull
        where TKey : notnull
    {
        var enumerator = new Cache.Internal.RemoveKeyEnumerator<TObject, TKey>(changeSetWithKey, list);

        return new ChangeSet<TObject>(enumerator);
    }
}
