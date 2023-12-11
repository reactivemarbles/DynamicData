// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Linq;

namespace DynamicData.List.Internal;

internal class ClonedListChangeSet<TObject>
    where TObject : notnull
{
    public ClonedListChangeSet(IObservable<IChangeSet<TObject>> source, IEqualityComparer<TObject>? equalityComparer, object synchronize) =>
        Source = source
                    .Synchronize(synchronize)
                    .Do(changeSet => List.Clone(changeSet, equalityComparer));

    public List<TObject> List { get; } = [];

    public IObservable<IChangeSet<TObject>> Source { get; }
}
