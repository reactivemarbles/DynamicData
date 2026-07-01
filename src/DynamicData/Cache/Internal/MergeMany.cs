// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace DynamicData.Cache.Internal;

internal sealed class MergeMany<TObject, TKey, TDestination>
    where TObject : notnull
    where TKey : notnull
{
    private readonly Func<TObject, TKey, IObservable<TDestination>> _observableSelector;
    private readonly IObservable<IChangeSet<TObject, TKey>> _source;

    public MergeMany(IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, TKey, IObservable<TDestination>> observableSelector)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _observableSelector = observableSelector ?? throw new ArgumentNullException(nameof(observableSelector));
    }

    public MergeMany(IObservable<IChangeSet<TObject, TKey>> source, Func<TObject, IObservable<TDestination>> observableSelector)
    {
        observableSelector.ThrowArgumentNullExceptionIfNull(nameof(observableSelector));

        _source = source ?? throw new ArgumentNullException(nameof(source));
        _observableSelector = (t, _) => observableSelector(t);
    }

    public IObservable<TDestination> Run() =>
        _source.Orchestrate<TObject, TKey, TDestination, TDestination>(
            onSourceChangeSet: (changes, context) =>
            {
                foreach (var change in changes.ToConcreteType())
                {
                    if (change.Reason is ChangeReason.Add or ChangeReason.Update)
                    {
                        context.Track(change.Key, _observableSelector(change.Current, change.Key));
                    }
                    else if (change.Reason is ChangeReason.Remove)
                    {
                        context.Untrack(change.Key);
                    }
                }
            },
            onInner: (value, _, emitter) => emitter.OnNext(value));
}
