// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

// ReSharper disable once CheckNamespace
namespace DynamicData;

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
    /// <param name="source">The <see cref="IObservable{T}"/> of buffered changeset lists.</param>
    /// <returns>A list changeset stream with all buffered changes concatenated into single changesets.</returns>
    /// <remarks>
    /// <para>Use this after applying <c>Observable.Buffer()</c> to a changeset stream to re-merge the batched changesets into a single stream.</para>
    /// </remarks>
    /// <seealso cref="BufferIf{T}(IObservable{IChangeSet{T}}, IObservable{bool}, IScheduler?)"/>
    /// <seealso cref="BufferInitial{T}(IObservable{IChangeSet{T}}, TimeSpan, IScheduler?)"/>
    public static IObservable<IChangeSet<T>> FlattenBufferResult<T>(this IObservable<IList<IChangeSet<T>>> source)
        where T : notnull => source.Where(x => x.Count != 0).Select(updates => new ChangeSet<T>(updates.SelectMany(u => u)));
}
