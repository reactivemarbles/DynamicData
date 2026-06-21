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
    /// Callback for each item as and when it is being added to the stream.
    /// </summary>
    /// <typeparam name="TObject">The type of the object.</typeparam>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;</c> to observe item additions in.</param>
    /// <param name="addAction">The <c>Action&lt;TObject, TKey&gt;</c> callback invoked for each added item. Receives the new item and its key.</param>
    /// <returns>A stream that forwards all changesets from <paramref name="source"/> unchanged.</returns>
    /// <remarks>
    /// <para>
    /// <b>Change reason handling:</b>
    /// <list type="table">
    ///   <listheader><term>Event</term><description>Behavior</description></listheader>
    ///   <item><term>Add</term><description>Invokes <paramref name="addAction"/> with the item and key.</description></item>
    ///   <item><term>Update</term><description>Ignored.</description></item>
    ///   <item><term>Remove</term><description>Ignored.</description></item>
    ///   <item><term>Refresh</term><description>Ignored.</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Exceptions thrown in <paramref name="addAction"/> propagate as <c>OnError</c>. No try-catch is applied.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="addAction"/> is <see langword="null"/>.</exception>
    /// <seealso><c>OnItemUpdated&lt;TObject,TKey&gt;(IObservable&lt;IChangeSet&lt;TObject,TKey&gt;&gt;, Action&lt;TObject,TObject,TKey&gt;)</c></seealso>
    /// <seealso><c>OnItemRemoved&lt;TObject,TKey&gt;(IObservable&lt;IChangeSet&lt;TObject,TKey&gt;&gt;, Action&lt;TObject,TKey&gt;, bool)</c></seealso>
    /// <seealso><c>ForEachChange&lt;TObject,TKey&gt;</c></seealso>
    /// <seealso><c>ObservableListEx.OnItemAdded</c></seealso>
    public static IObservable<IChangeSet<TObject, TKey>> OnItemAdded<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Action<TObject, TKey> addAction)
        where TObject : notnull
        where TKey : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(addAction);

        return source.OnChangeAction(ChangeReason.Add, addAction);
    }

    /// <summary>
    /// Provides an overload of <c>addAction</c> for the supplied arguments.
    /// </summary>
    /// <typeparam name="TObject">The type of the TObject value.</typeparam>
    /// <typeparam name="TKey">The type of the TKey value.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;</c> to observe item additions in.</param>
    /// <param name="addAction">The <c>Action&lt;TObject&gt;</c> callback invoked for each added item. Receives only the item (no key).</param>
    /// <returns>The resulting observable sequence.</returns>
    /// <remarks>Overload that omits the key from the callback. Delegates to <c>OnItemAdded&lt;TObject, TKey&gt;(IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;, Action&lt;TObject, TKey&gt;)</c>.</remarks>
    public static IObservable<IChangeSet<TObject, TKey>> OnItemAdded<TObject, TKey>(this IObservable<IChangeSet<TObject, TKey>> source, Action<TObject> addAction)
        where TObject : notnull
        where TKey : notnull
        => source.OnItemAdded((obj, _) => addAction(obj));
}
