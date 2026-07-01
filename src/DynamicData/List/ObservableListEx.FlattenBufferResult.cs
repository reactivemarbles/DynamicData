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
    /// Flattens buffered changesets back into single changesets.
    /// Empty buffers are dropped.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    /// <param name="source">The <c>IObservable&lt;T&gt;</c> of buffered changeset lists.</param>
    /// <returns>A list changeset stream with all buffered changes concatenated into single changesets.</returns>
    /// <remarks>
    /// <para>Use this after applying <c>Observable.Buffer()</c> to a changeset stream to re-merge the batched changesets into a single stream.</para>
    /// </remarks>
    /// <seealso><c>BufferIf&lt;T&gt;(IObservable&lt;IChangeSet&lt;T&gt;&gt;, IObservable&lt;bool&gt;, IScheduler?)</c></seealso>
    /// <seealso><c>BufferInitial&lt;T&gt;(IObservable&lt;IChangeSet&lt;T&gt;&gt;, TimeSpan, IScheduler?)</c></seealso>
    public static IObservable<IChangeSet<T>> FlattenBufferResult<T>(this IObservable<IList<IChangeSet<T>>> source)
        where T : notnull
    {
        ArgumentExceptionHelper.ThrowIfNull(source);
        return source.Where(x => x.Count != 0).Select(updates => new ChangeSet<T>(updates.SelectMany(u => u)));
    }
}
