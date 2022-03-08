// Copyright (c) 2011-2020 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Reactive.Linq;

using DynamicData.Kernel;

namespace DynamicData.List.Internal;

internal class QueryWhenChanged<T>
{
    private readonly IObservable<IChangeSet<T>> _source;

    public QueryWhenChanged(IObservable<IChangeSet<T>> source)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
    }

    public IObservable<IReadOnlyCollection<T>> Run()
    {
        return Observable.Create<IReadOnlyCollection<T>>(observer =>
        {
            var list = new List<T>();

            return _source.Subscribe(changes =>
            {
                list.Clone(changes);
                observer.OnNext(new ReadOnlyCollectionLight<T>(list));
            });
        });
    }
}
