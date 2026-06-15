// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.List.Internal;

/// <summary>
/// Holds an observable list changeset together with an aggregated mirror of its content.
/// Callers drive the mirror by invoking <see cref="Process"/> for each changeset they
/// observe through <see cref="Source"/>. See <see cref="DynamicData.Cache.Internal.ChangeSetCache{TObject, TKey}"/>
/// for the rationale.
/// </summary>
internal sealed class ClonedListChangeSet<TObject>(IObservable<IChangeSet<TObject>> source, IEqualityComparer<TObject>? equalityComparer)
    where TObject : notnull
{
    public List<TObject> List { get; } = [];

    public IObservable<IChangeSet<TObject>> Source { get; } = source;

    /// <summary>Applies <paramref name="changes"/> to the aggregated <see cref="List"/>.</summary>
    public void Process(IChangeSet<TObject> changes) => List.Clone(changes, equalityComparer);
}
