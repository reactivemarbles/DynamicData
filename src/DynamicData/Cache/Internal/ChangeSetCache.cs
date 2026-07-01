// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Cache.Internal;

/// <summary>
/// Holds an observable changeset alongside an aggregated mirror cache.
/// <see cref="Process"/> applies a changeset to <see cref="Cache"/>.
/// </summary>
/// <typeparam name="TObject">ChangeSet Object Type.</typeparam>
/// <typeparam name="TKey">ChangeSet Key Type.</typeparam>
internal sealed class ChangeSetCache<TObject, TKey>(IObservable<IChangeSet<TObject, TKey>> source)
    where TObject : notnull
    where TKey : notnull
{
    public Cache<TObject, TKey> Cache { get; } = new();

    public IObservable<IChangeSet<TObject, TKey>> Source { get; } = source;

    public void Process(IChangeSet<TObject, TKey> changes) => Cache.Clone(changes);
}
