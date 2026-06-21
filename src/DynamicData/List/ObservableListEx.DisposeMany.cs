// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
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
    /// Disposes items that implement <see cref="IDisposable"/> when they are removed, replaced, or cleared from the stream.
    /// All remaining tracked items are disposed when the stream finalizes (OnCompleted, OnError, or subscription disposal).
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="source">The source <c>IObservable&lt;IChangeSet&lt;T&gt;&gt;</c> to track for disposal on removal.</param>
    /// <returns>A continuation of the source changeset stream with disposal side effects applied.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// Items are cast to <see cref="IDisposable"/> and disposed after the changeset has been forwarded downstream.
    /// Items that do not implement <see cref="IDisposable"/> are silently ignored.
    /// </para>
    /// <list type="table">
    /// <listheader><term>Event</term><description>Behavior</description></listheader>
    /// <item><term><b>Add</b>/<b>AddRange</b></term><description>Items are tracked for future disposal. Changeset forwarded.</description></item>
    /// <item><term><b>Replace</b></term><description>The previous (replaced) item is disposed after the changeset is forwarded. The new item is tracked.</description></item>
    /// <item><term><b>Remove</b>/<b>RemoveRange</b></term><description>Removed items are disposed after the changeset is forwarded.</description></item>
    /// <item><term><b>Clear</b></term><description>All tracked items are disposed after the changeset is forwarded.</description></item>
    /// <item><term><b>Moved</b>/<b>Refresh</b></term><description>Forwarded. No disposal occurs.</description></item>
    /// <item><term>OnError/OnCompleted/Disposal</term><description>All remaining tracked items are disposed during finalization.</description></item>
    /// </list>
    /// <para><b>Worth noting:</b> Disposal happens after the changeset is delivered downstream, so subscribers see the change before items are disposed.</para>
    /// </remarks>
    /// <seealso><c>OnItemRemoved&lt;T&gt;(IObservable&lt;IChangeSet&lt;T&gt;&gt;, Action&lt;T&gt;, bool)</c></seealso>
    /// <seealso><c>SubscribeMany&lt;T&gt;(IObservable&lt;IChangeSet&lt;T&gt;&gt;, Func&lt;T, IDisposable&gt;)</c></seealso>
    /// <seealso><c>ObservableCacheEx.DisposeMany&lt;TObject, TKey&gt;(IObservable&lt;IChangeSet&lt;TObject, TKey&gt;&gt;)</c></seealso>
    public static IObservable<IChangeSet<T>> DisposeMany<T>(this IObservable<IChangeSet<T>> source)
        where T : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);

        return new DisposeMany<T>(source).Run();
    }
}
