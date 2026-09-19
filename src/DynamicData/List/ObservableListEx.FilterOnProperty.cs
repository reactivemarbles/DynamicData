// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Linq.Expressions;
#if REACTIVE_SHIM
using DynamicData.Reactive.List.Internal;
#else
using DynamicData.List.Internal;
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
    /// Filters items based on a property value, automatically re-evaluating when the specified property changes on any item.
    /// </summary>
    /// <typeparam name="TObject">The type of the object. Must implement <see cref="INotifyPropertyChanged"/>.</typeparam>
    /// <typeparam name="TProperty">The type of the property.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;TObject&gt;&gt;</c> to filter by property value.</param>
    /// <param name="propertySelector"><c>Expression&lt;TDelegate&gt;</c> selecting the property to monitor for changes.</param>
    /// <param name="predicate">A <c>Func&lt;T, TResult&gt;</c> predicate evaluated against the item to determine inclusion.</param>
    /// <param name="propertyChangedThrottle">An optional <see cref="TimeSpan"/> throttle duration for property change notifications.</param>
    /// <param name="scheduler">The <see cref="IScheduler"/> used when throttling.</param>
    /// <returns>A list changeset stream of items satisfying the predicate, re-evaluated on property changes.</returns>
    /// <remarks>
    /// <para>Deprecated. Use <c>AutoRefresh&lt;TObject, TProperty&gt;(IObservable&lt;IChangeSet&lt;TObject&gt;&gt;, Expression&lt;Func&lt;TObject, TProperty&gt;&gt;, TimeSpan?, TimeSpan?, IScheduler?)</c> followed by <c>Filter&lt;T&gt;(IObservable&lt;IChangeSet&lt;T&gt;&gt;, Func&lt;T, bool&gt;)</c> instead.</para>
    /// </remarks>
    /// <seealso><c>AutoRefresh&lt;TObject, TProperty&gt;(IObservable&lt;IChangeSet&lt;TObject&gt;&gt;, Expression&lt;Func&lt;TObject, TProperty&gt;&gt;, TimeSpan?, TimeSpan?, IScheduler?)</c></seealso>
    /// <seealso><c>Filter&lt;T&gt;(IObservable&lt;IChangeSet&lt;T&gt;&gt;, Func&lt;T, bool&gt;)</c></seealso>
    [Obsolete("Use AutoRefresh(), followed by Filter() instead")]
    public static IObservable<IChangeSet<TObject>> FilterOnProperty<TObject, TProperty>(this IObservable<IChangeSet<TObject>> source, Expression<Func<TObject, TProperty>> propertySelector, Func<TObject, bool> predicate, TimeSpan? propertyChangedThrottle = null, IScheduler? scheduler = null)
        where TObject : INotifyPropertyChanged
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(propertySelector);
        ArgumentExceptionHelper.ThrowIfNull(predicate);

        return new FilterOnProperty<TObject, TProperty>(source, propertySelector, predicate, propertyChangedThrottle, scheduler).Run();
    }
}
