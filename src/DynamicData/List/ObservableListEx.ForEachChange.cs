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
/// Extensions for ObservableList.
/// </summary>
public static partial class ObservableListEx
{
    /// <summary>
    /// Invokes <paramref name="action"/> once for every <c>Change&lt;T&gt;</c> in each changeset. Range changes
    /// (AddRange, RemoveRange, Clear) are delivered as a single <c>Change&lt;T&gt;</c>; they are not flattened into per-item changes.
    /// The changeset is forwarded downstream unchanged.
    /// </summary>
    /// <typeparam name="TObject">The type of items in the list.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;TObject&gt;&gt;</c> to observe each change in.</param>
    /// <param name="action">The action invoked for each <c>Change&lt;T&gt;</c>.</param>
    /// <returns>A continuation of the source changeset stream.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="action"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>This is a side-effect operator. It does not modify the changeset. If you need each individual item from range operations flattened out, use <c>ForEachItemChange&lt;TObject&gt;(IObservable&lt;IChangeSet&lt;TObject&gt;&gt;, Action&lt;ItemChange&lt;TObject&gt;&gt;)</c> instead.</para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term>Add/Replace/Remove/Moved/Refresh</term><description>Callback invoked with the <c>Change&lt;T&gt;</c> (single-item change). Changeset forwarded.</description></item>
    /// <item><term>AddRange/RemoveRange/Clear</term><description>Callback invoked once with the <c>Change&lt;T&gt;</c> containing the range (accessible via <c>Range</c> property). Changeset forwarded.</description></item>
    /// <item><term>OnError</term><description>If the callback throws, the exception propagates as OnError.</description></item>
    /// </list>
    /// </remarks>
    /// <seealso><c>ForEachItemChange&lt;TObject&gt;(IObservable&lt;IChangeSet&lt;TObject&gt;&gt;, Action&lt;ItemChange&lt;TObject&gt;&gt;)</c></seealso>
    /// <seealso><c>OnItemAdded&lt;T&gt;(IObservable&lt;IChangeSet&lt;T&gt;&gt;, Action&lt;T&gt;)</c></seealso>
    /// <seealso><c>OnItemRemoved&lt;T&gt;(IObservable&lt;IChangeSet&lt;T&gt;&gt;, Action&lt;T&gt;, bool)</c></seealso>
    /// <seealso><c>ObservableCacheEx.ForEachChange&lt;TObject, TKey&gt;(IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;, Action&lt;Change&lt;TObject, TKey&gt;&gt;)</c></seealso>
    public static IObservable<IChangeSet<TObject>> ForEachChange<TObject>(this IObservable<IChangeSet<TObject>> source, Action<Change<TObject>> action)
        where TObject : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        ArgumentExceptionHelper.ThrowIfNull(action);

        return source.Do(changes => changes.ForEach(action));
    }
}
