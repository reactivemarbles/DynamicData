// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.List.Internal;

/// <summary>
/// Holds an observable list changeset alongside an aggregated mirror list.
/// <see cref="Process"/> applies a changeset to <see cref="List"/>.
/// </summary>
internal sealed class ClonedListChangeSet<TObject>(IObservable<IChangeSet<TObject>> source, IEqualityComparer<TObject>? equalityComparer)
    where TObject : notnull
{
    public List<TObject> List { get; } = [];

    public IObservable<IChangeSet<TObject>> Source { get; } = source;

    public void Process(IChangeSet<TObject> changes) => List.Clone(changes, equalityComparer);
}
