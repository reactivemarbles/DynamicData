// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Cache.Internal;
#else

namespace DynamicData.Cache.Internal;
#endif

/// <summary>
/// Provides members for the ToObservableOptional class.
/// </summary>
/// <typeparam name="TObject">The type of the TObject value.</typeparam>
/// <typeparam name="TKey">The type of the TKey value.</typeparam>
/// <param name="source">The source value.</param>
/// <param name="key">The key value.</param>
/// <param name="equalityComparer">The equalityComparer value.</param>
internal sealed class ToObservableOptional<TObject, TKey>(IObservable<IChangeSet<TObject, TKey>> source, TKey key, IEqualityComparer<TObject>? equalityComparer = null)
    where TObject : notnull
    where TKey : notnull
{
    /// <summary>
    /// The _equalityComparer field.
    /// </summary>
    private readonly IEqualityComparer<TObject> _equalityComparer = equalityComparer ?? EqualityComparer<TObject>.Default;

    /// <summary>
    /// The _source field.
    /// </summary>
    private readonly IObservable<IChangeSet<TObject, TKey>> _source = source ?? throw new ArgumentNullException(nameof(source));

    /// <summary>
    /// The _key field.
    /// </summary>
    private readonly TKey _key = key;

    /// <summary>
    /// Executes the Run operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public IObservable<ReactiveUI.Primitives.Optional<TObject>> Run() => Observable.Create<ReactiveUI.Primitives.Optional<TObject>>(observer =>
    {
        var lastValue = ReactiveUI.Primitives.Optional<TObject>.None;

        return _source.Subscribe(
                    changes => lastValue = EmitChanges(changes, observer, lastValue),
                    observer.OnError,
                    observer.OnCompleted);
    });

    /// <summary>
    /// Executes the EmitChanges operation.
    /// </summary>
    /// <param name="changes">The changes value.</param>
    /// <param name="observer">The observer value.</param>
    /// <param name="lastValue">The lastValue value.</param>
    /// <returns>The result of the operation.</returns>
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
