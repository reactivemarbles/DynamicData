// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Linq;

using DynamicData.Kernel;

namespace DynamicData.Cache.Internal;

internal sealed class Cast<TSource, TKey, TDestination>(IObservable<IChangeSet<TSource, TKey>> source, Func<TSource, TDestination> converter)
    where TSource : notnull
    where TKey : notnull
    where TDestination : notnull
{
    private readonly Func<TSource, TDestination> _converter = converter ?? throw new ArgumentNullException(nameof(converter));

    private readonly IObservable<IChangeSet<TSource, TKey>> _source = source ?? throw new ArgumentNullException(nameof(source));

    public IObservable<IChangeSet<TDestination, TKey>> Run() => _source.Select(
            changes =>
            {
                var transformed = changes.ToConcreteType().Select(change => new Change<TDestination, TKey>(change.Reason, change.Key, _converter(change.Current), change.Previous.Convert(_converter), change.CurrentIndex, change.PreviousIndex));
                return new ChangeSet<TDestination, TKey>(transformed);
            });
}
