// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

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
    /// Filters the source changeset stream to a single key, emitting the current value each time it changes.
    /// Even emits the value on removal (the removed item's value).
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <c>IObservableCache&lt;TObject, TKey&gt;</c> to watch a single key in.</param>
    /// <param name="key">The <typeparamref name="TKey"/> key to observe.</param>
    /// <returns>An observable of the item's value whenever it changes for the specified key.</returns>
    /// <remarks>
    /// <para>
    /// Unlike <c>ToObservableOptional&lt;TObject, TKey&gt;(IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;, TKey, IEqualityComparer&lt;TObject&gt;?)</c>,
    /// this does not emit <c>ReactiveUI.Primitives.Optional.None&lt;T&gt;</c> on removal. It emits the removed item's value instead.
    /// If you need to distinguish presence from absence, use ToObservableOptional.
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add</term><description>Emits the added item's value.</description></item>
    /// <item><term>Update</term><description>Emits the new value.</description></item>
    /// <item><term>Remove</term><description>Emits the removed item's value (not <c>None</c>; use <c>ToObservableOptional&lt;TObject, TKey&gt;(IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;, TKey, IEqualityComparer&lt;TObject&gt;?)</c> if you need removal detection).</description></item>
    /// <item><term>Refresh</term><description>Emits the current value.</description></item>
    /// </list>
    /// <para><b>Worth noting:</b> No emission occurs if the key is not present at subscription time. Changes to other keys are ignored entirely.</para>
    /// </remarks>
    /// <seealso><c>Watch&lt;TObject, TKey&gt;(IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;, TKey)</c></seealso>
    /// <seealso><c>ToObservableOptional&lt;TObject, TKey&gt;(IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;, TKey, IEqualityComparer&lt;TObject&gt;?)</c></seealso>
    public static IObservable<TObject> WatchValue<TObject, TKey>(this IObservableCache<TObject, TKey> source, TKey key)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);

        return source.Watch(key).Select(u => u.Current);
    }

    /// <summary>
    /// Provides an overload of <c>WatchValue</c> for the supplied arguments.
    /// </summary>
    /// <typeparam name="TObject">The type of the TObject value.</typeparam>
    /// <typeparam name="TKey">The type of the TKey value.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;</c> to watch a single key in.</param>
    /// <param name="key">The <typeparamref name="TKey"/> key to observe.</param>
    /// <returns>The resulting observable sequence.</returns>
    /// <remarks>This overload extends <c>IObservable</c>&lt;<c>IChangeSet&lt;TObject, TKey&gt;</c>&gt; instead of <c>IObservableCache&lt;TObject, TKey&gt;</c>.</remarks>
    public static IObservable<TObject> WatchValue<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, TKey key)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);

        return source.Watch(key).Select(u => u.Current);
    }
}
