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
/// Extensions for dynamic data.
/// </summary>
public static partial class ObservableCacheEx
{
    /// <summary>
    /// Emits an item whenever any of its properties change via <see cref="INotifyPropertyChanged"/>.
    /// Subscribes to PropertyChanged on each cache item using MergeMany.
    /// </summary>
    /// <typeparam name="TObject">The type of the object (must implement <see cref="INotifyPropertyChanged"/>).</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;</c> to observe property changes on items in.</param>
    /// <param name="propertiesToMonitor">The specific property names to monitor.  If empty, all property changes trigger emissions.</param>
    /// <returns>An observable that emits the item itself each time a monitored property changes.</returns>
    /// <remarks>
    /// <para>
    /// Subscriptions are managed per item: created on Add, replaced on Update, disposed on Remove.
    /// Errors from individual property subscriptions are silently ignored. The output is not a changeset
    /// stream; it is a plain <c>IObservable&lt;TObject?&gt;</c>. If the same item changes multiple properties
    /// rapidly, each change emits the item separately (no deduplication).
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>Subscribes to PropertyChanged on the new item.</description></item>
    /// <item><term>Update</term><description>Disposes the old item's subscription and subscribes to the new item.</description></item>
    /// <item><term>Remove</term><description>Disposes the item's PropertyChanged subscription.</description></item>
    /// <item><term>Refresh</term><description>No effect on subscriptions.</description></item>
    /// <item><term>OnError</term><description>Errors from individual property subscriptions are silently ignored. Source errors terminate the stream.</description></item>
    /// </list>
    /// </remarks>
    /// <seealso><c>WhenPropertyChanged&lt;TObject, TKey, TValue&gt;</c></seealso>
    /// <seealso><c>WhenValueChanged&lt;TObject, TKey, TValue&gt;</c></seealso>
    /// <seealso><c>AutoRefresh&lt;TObject, TKey&gt;(IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;, TimeSpan?, TimeSpan?, IScheduler?)</c></seealso>
    /// <seealso><c>ObservableListEx.WhenAnyPropertyChanged</c></seealso>
    public static IObservable<TObject?> WhenAnyPropertyChanged<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, params string[] propertiesToMonitor)
        where TObject : INotifyPropertyChanged
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);

        return source.MergeMany(t => t.WhenAnyPropertyChanged(propertiesToMonitor));
    }
}
