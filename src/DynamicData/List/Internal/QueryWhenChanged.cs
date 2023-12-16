// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Linq;

using DynamicData.Kernel;

namespace DynamicData.List.Internal;

internal sealed class QueryWhenChanged<T>(IObservable<IChangeSet<T>> source)
    where T : notnull
{
    private readonly IObservable<IChangeSet<T>> _source = source ?? throw new ArgumentNullException(nameof(source));

    public IObservable<IReadOnlyCollection<T>> Run() => Observable.Create<IReadOnlyCollection<T>>(observer =>
                                                             {
                                                                 var list = new List<T>();

                                                                 return _source.Subscribe(changes =>
                                                                 {
                                                                     list.Clone(changes);
                                                                     observer.OnNext(new ReadOnlyCollectionLight<T>(list));
                                                                 });
                                                             });
}
