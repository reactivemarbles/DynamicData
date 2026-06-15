// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using DynamicData.Binding;
using DynamicData.Cache;
using DynamicData.Cache.Internal;

// ReSharper disable once CheckNamespace

namespace DynamicData;

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
    /// <param name="source">The source <see cref="IObservable{IChangeSet{TObject, TKey}}"/> to observe property changes on items in.</param>
    /// <param name="propertiesToMonitor">The specific property names to monitor.  If empty, all property changes trigger emissions.</param>
    /// <returns>An observable that emits the item itself each time a monitored property changes.</returns>
    /// <remarks>
    /// <para>
    /// Subscriptions are managed per item: created on Add, replaced on Update, disposed on Remove.
    /// The output is not a changeset stream; it is a plain <c>IObservable&lt;TObject?&gt;</c>. If the same item changes multiple properties
    /// rapidly, each change emits the item separately (no deduplication).
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>Subscribes to PropertyChanged on the new item.</description></item>
    /// <item><term>Update</term><description>Disposes the old item's subscription and subscribes to the new item.</description></item>
    /// <item><term>Remove</term><description>Disposes the item's PropertyChanged subscription.</description></item>
    /// <item><term>Refresh</term><description>No effect on subscriptions.</description></item>
    /// <item><term>OnError</term><description>Errors from any item's property subscription terminate the output stream. Source errors also terminate the stream.</description></item>
    /// </list>
    /// </remarks>
    /// <seealso cref="WhenPropertyChanged{TObject, TKey, TValue}"/>
    /// <seealso cref="WhenValueChanged{TObject, TKey, TValue}"/>
    /// <seealso cref="AutoRefresh{TObject, TKey}(IObservable{IChangeSet{TObject, TKey}}, TimeSpan?, TimeSpan?, IScheduler?)"/>
    /// <seealso cref="ObservableListEx.WhenAnyPropertyChanged"/>
    public static IObservable<TObject?> WhenAnyPropertyChanged<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, params string[] propertiesToMonitor)
        where TObject : INotifyPropertyChanged
        where TKey : notnull
    {
        source.ThrowArgumentNullExceptionIfNull(nameof(source));

        return source.MergeMany(t => t.WhenAnyPropertyChanged(propertiesToMonitor));
    }
}
