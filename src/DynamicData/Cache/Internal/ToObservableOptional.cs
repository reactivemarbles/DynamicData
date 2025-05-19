// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Linq;

namespace DynamicData.Cache.Internal;

internal sealed class ToObservableOptional<TObject, TKey>(IObservable<IChangeSet<TObject, TKey>> source, TKey key, IEqualityComparer<TObject>? equalityComparer = null)
    where TObject : notnull
    where TKey : notnull
{
    private readonly IEqualityComparer<TObject> _equalityComparer = equalityComparer ?? EqualityComparer<TObject>.Default;
    private readonly IObservable<IChangeSet<TObject, TKey>> _source = source ?? throw new ArgumentNullException(nameof(source));
    private readonly TKey _key = key;

    public IObservable<Optional<TObject>> Run() => Observable.Create<Optional<TObject>>(observer =>
    {
        var lastValue = Optional.None<TObject>();

        return _source.Subscribe(
                    changes => lastValue = EmitChanges(changes, observer, lastValue),
                    observer.OnError,
                    observer.OnCompleted);
    });

    private Optional<TObject> EmitChanges(IChangeSet<TObject, TKey> changes, IObserver<Optional<TObject>> observer, Optional<TObject> lastValue)
    {
        foreach (var change in changes.ToConcreteType())
        {
            // Ignore changes for different keys
            if (!change.Key.Equals(_key))
            {
                continue;
            }

            // Remove is None, everything else is the current value
            var emitValue = change switch
            {
                { Reason: ChangeReason.Remove } => Optional.None<TObject>(),
                _ => Optional.Some(change.Current),
            };

            // Emit the value if it has changed
            if ((emitValue.HasValue != lastValue.HasValue) || (emitValue.HasValue && !_equalityComparer.Equals(lastValue.Value, emitValue.Value)))
            {
                observer.OnNext(emitValue);
                lastValue = emitValue;
            }
        }

        return lastValue;
    }
}
