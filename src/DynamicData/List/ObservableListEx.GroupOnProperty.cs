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
    /// Groups items by a property value, automatically re-grouping when the specified property changes on any item.
    /// Each group contains an inner observable list.
    /// </summary>
    /// <typeparam name="TObject">The type of the object. Must implement <see cref="INotifyPropertyChanged"/>.</typeparam>
    /// <typeparam name="TGroup">The type of the group key.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;TObject&gt;&gt;</c> to group by property value.</param>
    /// <param name="propertySelector"><c>Expression&lt;TDelegate&gt;</c> selecting the property whose value determines the group key.</param>
    /// <param name="propertyChangedThrottle">An optional <see cref="TimeSpan"/> throttle duration for property change notifications.</param>
    /// <param name="scheduler">The <see cref="IScheduler"/> used when throttling.</param>
    /// <returns>A list changeset stream of <c>IGroup&lt;TObject, TGroup&gt;</c> objects.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="propertySelector"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// Convenience operator equivalent to <c>.AutoRefresh(propertySelector).GroupOn(item => property)</c>.
    /// Property changes trigger re-evaluation of the group key, potentially moving items between groups.
    /// </para>
    /// </remarks>
    /// <seealso><c>GroupOn&lt;TObject, TGroup&gt;(IObservable&lt;IChangeSet&lt;TObject&gt;&gt;, Func&lt;TObject, TGroup&gt;, IObservable&lt;Unit&gt;?)</c></seealso>
    /// <seealso><c>GroupOnPropertyWithImmutableState&lt;TObject, TGroup&gt;(IObservable&lt;IChangeSet&lt;TObject&gt;&gt;, Expression&lt;Func&lt;TObject, TGroup&gt;&gt;, TimeSpan?, IScheduler?)</c></seealso>
    /// <seealso><c>AutoRefresh&lt;TObject, TProperty&gt;(IObservable&lt;IChangeSet&lt;TObject&gt;&gt;, Expression&lt;Func&lt;TObject, TProperty&gt;&gt;, TimeSpan?, TimeSpan?, IScheduler?)</c></seealso>
    public static IObservable<IChangeSet<IGroup<TObject, TGroup>>> GroupOnProperty<TObject, TGroup>(this IObservable<IChangeSet<TObject>> source, Expression<Func<TObject, TGroup>> propertySelector, TimeSpan? propertyChangedThrottle = null, IScheduler? scheduler = null)
        where TObject : INotifyPropertyChanged
        where TGroup : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(propertySelector);

        return new GroupOnProperty<TObject, TGroup>(source, propertySelector, propertyChangedThrottle, scheduler).Run();
    }
}
