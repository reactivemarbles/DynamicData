// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Linq.Expressions;
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
    /// Watches all items in the source list and emits the item when any of its properties change.
    /// Requires <typeparamref name="TObject"/> to implement <see cref="INotifyPropertyChanged"/>.
    /// This is NOT a changeset operator: it returns a flat <c>IObservable&lt;T&gt;</c>.
    /// </summary>
    /// <typeparam name="TObject">The type of the object. Must implement <see cref="INotifyPropertyChanged"/>.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;TObject&gt;&gt;</c> to observe property changes on items in.</param>
    /// <param name="propertiesToMonitor">An optional list of property names to monitor. If empty, all property changes are observed.</param>
    /// <returns>An observable emitting the item whenever any monitored property changes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>Implemented via <c>MergeMany&lt;T, TDestination&gt;(IObservable&lt;IChangeSet&lt;T&gt;&gt;, Func&lt;T, IObservable&lt;TDestination&gt;&gt;)</c>. Subscriptions are managed per item: created on add, disposed on remove.</para>
    /// </remarks>
    /// <seealso><c>WhenPropertyChanged&lt;TObject, TValue&gt;(IObservable&lt;IChangeSet&lt;TObject&gt;&gt;, Expression&lt;Func&lt;TObject, TValue&gt;&gt;, bool)</c></seealso>
    /// <seealso><c>WhenValueChanged&lt;TObject, TValue&gt;(IObservable&lt;IChangeSet&lt;TObject&gt;&gt;, Expression&lt;Func&lt;TObject, TValue&gt;&gt;, bool)</c></seealso>
    /// <seealso><c>AutoRefresh&lt;TObject&gt;(IObservable&lt;IChangeSet&lt;TObject&gt;&gt;, TimeSpan?, TimeSpan?, IScheduler?)</c></seealso>
    /// <seealso><c>ObservableCacheEx.WhenAnyPropertyChanged&lt;TObject, TKey&gt;(IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;, string[])</c></seealso>
    public static IObservable<TObject?> WhenAnyPropertyChanged<TObject>(this IObservable<IChangeSet<TObject>> source, params string[] propertiesToMonitor)
        where TObject : INotifyPropertyChanged
    {
        ArgumentExceptionHelper.ThrowIfNull(source);

        return source.MergeMany(t => t.WhenAnyPropertyChanged(propertiesToMonitor));
    }
}
