// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Linq;

namespace DynamicData.List.Internal;

internal sealed class QueryWhenChanged<T>(IObservable<IChangeSet<T>> source)
    where T : notnull
{
    private readonly IObservable<IChangeSet<T>> _source = source ?? throw new ArgumentNullException(nameof(source));

    public IObservable<IReadOnlyCollection<T>> Run() => Observable.Defer(() =>
    {
        var list = new List<T>();

        return _source.Select(changes =>
        {
            list.Clone(changes);
            return (IReadOnlyCollection<T>)new ReadOnlyCollectionLight<T>(list);
        });
    });
}
