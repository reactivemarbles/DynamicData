// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Cache.Internal;
#else

namespace DynamicData.Cache.Internal;
#endif

/// <summary>
/// Wraps an Observable ChangeSet while maintaining a copy of the aggregated changes.
/// </summary>
/// <typeparam name="TObject">ChangeSet Object Type.</typeparam>
/// <typeparam name="TKey">ChangeSet Key Type.</typeparam>
internal sealed class ChangeSetCache<TObject, TKey>
    where TObject : notnull
    where TKey : notnull
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ChangeSetCache{TObject, TKey}"/> class.
    /// </summary>
    /// <param name="source">The source value.</param>
    public ChangeSetCache(IObservable<IChangeSet<TObject, TKey>> source) =>
        Source = source.Do(Cache.Clone);

    /// <summary>
    /// Gets the Cache value.
    /// </summary>
    public Cache<TObject, TKey> Cache { get; } = new();

    /// <summary>
    /// Gets the Source value.
    /// </summary>
    public IObservable<IChangeSet<TObject, TKey>> Source { get; }
}
