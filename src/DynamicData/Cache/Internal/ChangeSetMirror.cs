// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Cache.Internal;

/// <summary>
/// Pairs a child changeset stream with a live mirror of the items it currently holds.
/// Merge operators keep one of these per child so they can answer "what does this child
/// hold right now?" when resolving a key that more than one child publishes.
/// <see cref="Process"/> applies an incoming changeset to <see cref="Cache"/>.
/// </summary>
/// <typeparam name="TObject">ChangeSet Object Type.</typeparam>
/// <typeparam name="TKey">ChangeSet Key Type.</typeparam>
internal sealed class ChangeSetMirror<TObject, TKey>(IObservable<IChangeSet<TObject, TKey>> source)
    where TObject : notnull
    where TKey : notnull
{
    public Cache<TObject, TKey> Cache { get; } = new();

    public IObservable<IChangeSet<TObject, TKey>> Source { get; } = source;

    public void Process(IChangeSet<TObject, TKey> changes) => Cache.Clone(changes);
}
