// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Cache.Internal;

/// <summary>
/// Holds an observable changeset together with an aggregated mirror of its content.
/// Callers drive the mirror by invoking <see cref="Process"/> for each changeset they
/// observe through <see cref="Source"/>, on whatever thread they've already serialized
/// delivery to. The mirror is not updated automatically, so consumers do not need to
/// pre-wrap <paramref name="source"/> in a synchronization layer for the sole purpose
/// of keeping the mirror coherent.
/// </summary>
/// <typeparam name="TObject">ChangeSet Object Type.</typeparam>
/// <typeparam name="TKey">ChangeSet Key Type.</typeparam>
internal sealed class ChangeSetCache<TObject, TKey>(IObservable<IChangeSet<TObject, TKey>> source)
    where TObject : notnull
    where TKey : notnull
{
    public Cache<TObject, TKey> Cache { get; } = new();

    public IObservable<IChangeSet<TObject, TKey>> Source { get; } = source;

    /// <summary>Applies <paramref name="changes"/> to the aggregated <see cref="Cache"/>.</summary>
    public void Process(IChangeSet<TObject, TKey> changes) => Cache.Clone(changes);
}
