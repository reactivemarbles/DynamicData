// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Linq;

namespace DynamicData.Cache.Internal;

internal sealed class MergeManyItems<TObject, TKey, TDestination>
    where TObject : notnull
    where TKey : notnull
{
    private readonly Func<TObject, TKey, IObservable<TDestination>> _observableSelector;

    private readonly IObservable<IChangeSet<TObject, TKey>> _source;

    public MergeManyItems(IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IObservable<TDestination>> observableSelector)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _observableSelector = observableSelector ?? throw new ArgumentNullException(nameof(observableSelector));
    }

    public MergeManyItems(IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IObservable<TDestination>> observableSelector)
    {
        if (observableSelector is null)
        {
            throw new ArgumentNullException(nameof(observableSelector));
        }

        _source = source ?? throw new ArgumentNullException(nameof(source));
        _observableSelector = (t, _) => observableSelector(t);
    }

    // MergeMany already tracks the parent and every child subscription so that the result finishes only once
    // all of them have. Reusing it keeps a child completing from terminating the whole stream.
    public IObservable<ItemWithValue<TObject, TDestination>> Run() =>
        new MergeMany<TObject, TKey, ItemWithValue<TObject, TDestination>>(
            _source,
            (t, v) => _observableSelector(t, v).Select(z => new ItemWithValue<TObject, TDestination>(t, z))).Run();
}
