// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Linq;

namespace DynamicData.Cache.Internal;

/// <summary>
/// Wraps an Observable ChangeSet while maintaining a copy of the aggregated changes.
/// </summary>
/// <typeparam name="TObject">ChangeSet Object Type.</typeparam>
/// <typeparam name="TKey">ChangeSet Key Type.</typeparam>
internal sealed class ChangeSetCache<TObject, TKey>
    where TObject : notnull
    where TKey : notnull
{
    public ChangeSetCache(IObservable<IChangeSet<TObject, TKey>> source) =>
        Source = source.IgnoreSameReferenceUpdate().Do(Cache.Clone);

    public Cache<TObject, TKey> Cache { get; } = new();

    public IObservable<IChangeSet<TObject, TKey>> Source { get; }
}
