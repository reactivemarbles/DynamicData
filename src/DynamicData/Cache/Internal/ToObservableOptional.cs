// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Cache.Internal;
#else

namespace DynamicData.Cache.Internal;
#endif

internal sealed class ToObservableOptional<TObject, TKey>(IObservable<IChangeSet<TObject, TKey>> source, TKey key, IEqualityComparer<TObject>? equalityComparer = null)
    where TObject : notnull
    where TKey : notnull
{
    private readonly IEqualityComparer<TObject> _equalityComparer = equalityComparer ?? EqualityComparer<TObject>.Default;
    private readonly IObservable<IChangeSet<TObject, TKey>> _source = source ?? throw new ArgumentNullException(nameof(source));
    private readonly TKey _key = key;

    public IObservable<ReactiveUI.Primitives.Optional<TObject>> Run() => Observable.Create<ReactiveUI.Primitives.Optional<TObject>>(observer =>
    {
        var lastValue = ReactiveUI.Primitives.Optional<TObject>.None;

        return _source.Subscribe(
                    changes => lastValue = EmitChanges(changes, observer, lastValue),
                    observer.OnError,
                    observer.OnCompleted);
    });

    private ReactiveUI.Primitives.Optional<TObject> EmitChanges(IChangeSet<TObject, TKey> changes, IObserver<ReactiveUI.Primitives.Optional<TObject>> observer, ReactiveUI.Primitives.Optional<TObject> lastValue)
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
                { Reason: ChangeReason.Remove } => ReactiveUI.Primitives.Optional<TObject>.None,
                _ => ReactiveUI.Primitives.Optional.Some(change.Current),
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
