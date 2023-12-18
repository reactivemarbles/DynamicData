// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Linq;

namespace DynamicData.List.Internal;

internal sealed class ClonedListChangeSet<TObject>
    where TObject : notnull
{
    public ClonedListChangeSet(IObservable<IChangeSet<TObject>> source, IEqualityComparer<TObject>? equalityComparer) =>
        Source = source.Do(changeSet => List.Clone(changeSet, equalityComparer));

    public List<TObject> List { get; } = [];

    public IObservable<IChangeSet<TObject>> Source { get; }
}
