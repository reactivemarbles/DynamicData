// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Linq;

namespace DynamicData.List.Internal;

internal class ChangeSetCache<TObject>
    where TObject : notnull
{
    public ChangeSetCache(IObservable<IChangeSet<TObject>> source) =>
        Source = source.Do(List.Clone);

    public List<TObject> List { get; } = [];

    public IObservable<IChangeSet<TObject>> Source { get; }
}
