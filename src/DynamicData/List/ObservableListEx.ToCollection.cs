// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

using DynamicData.Reactive.Binding;
#else

using DynamicData.Binding;
#endif

// ReSharper disable once CheckNamespace
#if REACTIVE_SHIM
namespace DynamicData.Reactive;
#else
namespace DynamicData;
#endif

/// <summary>
/// Extensions for ObservableList.
/// </summary>
public static partial class ObservableListEx
{
    /// <summary>
    /// Emits the full collection as an <c>IReadOnlyCollection&lt;T&gt;</c> after every changeset. Equivalent to <c>QueryWhenChanged(items => items)</c>.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the list.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;TObject&gt;&gt;</c> to materialize into a collection on each change.</param>
    /// <returns>An observable emitting the full collection snapshot after each change.</returns>
    /// <seealso><c>QueryWhenChanged&lt;T&gt;(IObservable&lt;IChangeSet&lt;T&gt;&gt;)</c></seealso>
    /// <seealso><c>ToSortedCollection&lt;TObject, TSortKey&gt;(IObservable&lt;IChangeSet&lt;TObject&gt;&gt;, Func&lt;TObject, TSortKey&gt;, SortDirection)</c></seealso>
    /// <seealso><c>ObservableCacheEx.ToCollection&lt;TObject, TKey&gt;(IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;)</c></seealso>
    public static IObservable<IReadOnlyCollection<TObject>> ToCollection<TObject>(this IObservable<IChangeSet<TObject>> source)
        where TObject : notnull => source.QueryWhenChanged(items => items);
}
