// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.List.Internal;
#else

namespace DynamicData.List.Internal;
#endif

/// <summary>
/// Provides members for the ClonedListChangeSet class.
/// </summary>
/// <typeparam name="TObject">The type of the TObject value.</typeparam>
internal sealed class ClonedListChangeSet<TObject>
    where TObject : notnull
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ClonedListChangeSet{TObject}"/> class.
    /// </summary>
    /// <param name="source">The source value.</param>
    /// <param name="equalityComparer">The equalityComparer value.</param>
    public ClonedListChangeSet(IObservable<IChangeSet<TObject>> source, IEqualityComparer<TObject>? equalityComparer) =>
        Source = source.Do(changeSet => List.Clone(changeSet, equalityComparer));

    /// <summary>
    /// Gets the List value.
    /// </summary>
    public List<TObject> List { get; } = [];

    /// <summary>
    /// Gets the Source value.
    /// </summary>
    public IObservable<IChangeSet<TObject>> Source { get; }
}
