// Copyright (c) 2011-2020 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Reactive.Linq;

using DynamicData.Kernel;

namespace DynamicData.List.Internal
{
    internal class QueryWhenChanged<T>
    {
        private readonly IObservable<IChangeSet<T>> _source;

        public QueryWhenChanged(IObservable<IChangeSet<T>> source)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
        }

        public IObservable<IReadOnlyCollection<T>> Run()
        {
            return _source.Scan(
                new List<T>(),
                (list, changes) =>
                    {
                        list.Clone(changes);
                        return list;
                    }).Select(list => new ReadOnlyCollectionLight<T>(list));
        }
    }
}