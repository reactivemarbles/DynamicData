// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

using DynamicData.Reactive.Cache.Internal;
#else

using DynamicData.Cache.Internal;
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
    /// Casts each item in the changeset to a new type using the provided converter function.
    /// Equivalent to <c>Transform&lt;TDestination, TObject, TKey&gt;(IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;, Func&lt;TObject, TDestination&gt;, bool)</c>
    /// but named for discoverability when a simple type cast or conversion is needed.
    /// </summary>
    /// <typeparam name="TSource">The type of the source object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TDestination">The type of the destination object.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;TSource, TKey&gt;&gt;</c> to cast.</param>
    /// <param name="converter">The <c>Func&lt;TSource, TDestination&gt;</c> conversion function applied to each item.</param>
    /// <returns>An observable changeset of converted items.</returns>
    /// <remarks>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>Calls <paramref name="converter"/> and emits an <b>Add</b> with the converted item.</description></item>
    /// <item><term>Update</term><description>Calls <paramref name="converter"/> on the new value and emits an <b>Update</b>.</description></item>
    /// <item><term>Remove</term><description>Emits a <b>Remove</b>. The converter is not called.</description></item>
    /// <item><term>Refresh</term><description>Forwarded as <b>Refresh</b>. The converter is not called.</description></item>
    /// </list>
    /// </remarks>
    /// <seealso><c>OfType&lt;TObject, TKey, TDestination&gt;</c></seealso>
    public static IObservable<IChangeSet<TDestination, TKey>> Cast<TSource, TKey, TDestination>(this IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TDestination> converter)
        where TSource : notnull
        where TKey : notnull
        where TDestination : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);

        return new Cast<TSource, TKey, TDestination>(source, converter).Run();
    }
}
