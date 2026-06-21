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
    /// Optimized transform for immutable items with deterministic (pure) transform functions.
    /// Refresh changes are dropped entirely since immutable items cannot change in place.
    /// </summary>
    /// <typeparam name="TDestination">The type of the transformed items.</typeparam>
    /// <typeparam name="TSource">The type of the source items.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;TSource, TKey&gt;&gt;</c> to transform (items assumed immutable).</param>
    /// <param name="transformFactory">The <c>Func&lt;TSource, TDestination&gt;</c> pure function that maps a source item to a destination item. Must be deterministic: same input always produces equivalent output.</param>
    /// <returns>An observable changeset of transformed items.</returns>
    /// <remarks>
    /// <para>
    /// Because the transform is assumed to be stateless and deterministic, this operator does not track
    /// previously transformed items. This reduces memory overhead compared to <c>Transform&lt;TDestination, TSource, TKey&gt;(IObservable&lt;IChangeSet&lt;TSource, TKey&gt;&gt;, Func&lt;TSource, Optional&lt;TSource&gt;, TKey, TDestination&gt;, IObservable&lt;Func&lt;TSource, TKey, bool&gt;&gt;?)</c>.
    /// </para>
    /// <para><b>Change reason handling:</b></para>
    /// <list type="table">
    ///   <listheader><term>Input reason</term><description>Output behavior</description></listheader>
    ///   <item><term>Add</term><description>Calls factory, emits Add.</description></item>
    ///   <item><term>Update</term><description>Calls factory, emits Update.</description></item>
    ///   <item><term>Remove</term><description>Emits Remove. Factory is NOT called.</description></item>
    ///   <item><term>Refresh</term><description>DROPPED. Immutable items do not change, so Refresh is meaningless.</description></item>
    /// </list>
    /// <para>Use this when items are immutable, the factory is pure, and the factory is cheap. If any of these conditions are false, use <c>Transform&lt;TDestination, TSource, TKey&gt;(IObservable&lt;IChangeSet&lt;TSource, TKey&gt;&gt;, Func&lt;TSource, Optional&lt;TSource&gt;, TKey, TDestination&gt;, IObservable&lt;Func&lt;TSource, TKey, bool&gt;&gt;?)</c> instead.</para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="transformFactory"/> is <see langword="null"/>.</exception>
    public static IObservable<IChangeSet<TDestination, TKey>> TransformImmutable<TDestination, TSource, TKey>(
            this IObservable<IChangeSet<TSource, TKey>> source,
            Func<TSource, TDestination> transformFactory)
        where TDestination : notnull
        where TSource : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(transformFactory);

        return new TransformImmutable<TDestination, TSource, TKey>(
                source: source,
                transformFactory: transformFactory)
            .Run();
    }
}
