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
    /// Subscribes to a per-item observable for each item in the source and merges all emissions into a single <c>IObservable&lt;TDestination&gt;</c> stream.
    /// This is NOT a changeset operator: it returns a flat observable of values.
    /// </summary>
    /// <typeparam name="T">The type of items in the source list.</typeparam>
    /// <typeparam name="TDestination">The type of values emitted by per-item observables.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;T&gt;&gt;</c> whose items each produce an observable.</param>
    /// <param name="observableSelector">A <c>Func&lt;T, TResult&gt;</c> function that returns an observable for each source item.</param>
    /// <returns>An observable that emits values from all per-item observables, merged together.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="observableSelector"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <list type="table">
    /// <listheader><term>Event (source)</term><description>Subscription behavior</description></listheader>
    /// <item><term><b>Add</b>/<b>AddRange</b></term><description>Subscribes to the per-item observable. Emissions are merged into the output.</description></item>
    /// <item><term><b>Replace</b></term><description>Old subscription disposed, new subscription created for the replacement item.</description></item>
    /// <item><term><b>Remove</b>/<b>RemoveRange</b>/<b>Clear</b></term><description>Subscription disposed.</description></item>
    /// <item><term><b>Refresh</b>/<b>Moved</b></term><description>No effect on subscriptions.</description></item>
    /// <item><term>OnCompleted (source)</term><description>Completes only after the source and all active inner observables have completed.</description></item>
    /// </list>
    /// </remarks>
    /// <seealso><c>SubscribeMany&lt;T&gt;(IObservable&lt;IChangeSet&lt;T&gt;&gt;, Func&lt;T, IDisposable&gt;)</c></seealso>
    /// <seealso><c>MergeManyChangeSets&lt;TObject, TDestination&gt;(IObservable&lt;IChangeSet&lt;TObject&gt;&gt;, Func&lt;TObject, IObservable&lt;IChangeSet&lt;TDestination&gt;&gt;&gt;, IEqualityComparer&lt;TDestination&gt;?)</c></seealso>
    /// <seealso><c>WhenPropertyChanged&lt;TObject, TValue&gt;(IObservable&lt;IChangeSet&lt;TObject&gt;&gt;, Expression&lt;Func&lt;TObject, TValue&gt;&gt;, bool)</c></seealso>
    /// <seealso><c>ObservableCacheEx.MergeMany&lt;TObject, TKey, TDestination&gt;(IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;, Func&lt;TObject, IObservable&lt;TDestination&gt;&gt;)</c></seealso>
    public static IObservable<TDestination> MergeMany<T, TDestination>(this IObservable<IChangeSet<T>> source, Func<T, IObservable<TDestination>> observableSelector)
        where T : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(observableSelector);

        return new MergeMany<T, TDestination>(source, observableSelector).Run();
    }
}
