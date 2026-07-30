// Copyright (c) 2011-2025 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
#if REACTIVE_SHIM

namespace DynamicData.Reactive.Cache.Internal;
#else

namespace DynamicData.Cache.Internal;
#endif

/// <summary>
/// Provides members for the EditDiffChangeSetOptional class.
/// </summary>
/// <typeparam name="TObject">The type of the TObject value.</typeparam>
/// <typeparam name="TKey">The type of the TKey value.</typeparam>
/// <param name="source">The source value.</param>
/// <param name="keySelector">The keySelector value.</param>
/// <param name="equalityComparer">The equalityComparer value.</param>
internal sealed class EditDiffChangeSetOptional<TObject, TKey>(IObservable<ReactiveUI.Primitives.Optional<TObject>> source, Func<TObject, TKey> keySelector, IEqualityComparer<TObject>? equalityComparer)
    where TObject : notnull
    where TKey : notnull
{
    /// <summary>
    /// The _source field.
    /// </summary>
    private readonly IObservable<ReactiveUI.Primitives.Optional<TObject>> _source = source ?? throw new ArgumentNullException(nameof(source));

    /// <summary>
    /// The _equalityComparer field.
    /// </summary>
    private readonly IEqualityComparer<TObject> _equalityComparer = equalityComparer ?? EqualityComparer<TObject>.Default;

    /// <summary>
    /// The _keySelector field.
    /// </summary>
    private readonly Func<TObject, TKey> _keySelector = keySelector ?? throw new ArgumentNullException(nameof(keySelector));

    /// <summary>
    /// Executes the Run operation.
    /// </summary>
    /// <returns>The result of the operation.</returns>
    public IObservable<IChangeSet<TObject, TKey>> Run() => Observable.Create<IChangeSet<TObject, TKey>>(observer =>
                                                                {
                                                                    var previous = ReactiveUI.Primitives.Optional<ValueContainer>.None;

                                                                    return _source.Synchronize().Subscribe(
                                                                        nextValue =>
                                                                        {
                                                                            var current = nextValue.Convert(val => new ValueContainer(val, _keySelector(val)));

                                                                            // Determine the changes
                                                                            var changes = (previous.HasValue, current.HasValue) switch
                                                                            {
                                                                                (true, true) => CreateUpdateChanges(previous.Value, current.Value),
                                                                                (false, true) => [new Change<TObject, TKey>(ChangeReason.Add, current.Value.Key, current.Value.Object)],
                                                                                (true, false) => [new Change<TObject, TKey>(ChangeReason.Remove, previous.Value.Key, previous.Value.Object)],
                                                                                (false, false) => [],
                                                                            };

                                                                            // Save the value for the next round
                                                                            previous = current;

                                                                            // If there are changes, emit as a ChangeSet
                                                                            if (changes.Length > 0)
                                                                            {
                                                                                observer.OnNext(new ChangeSet<TObject, TKey>(changes));
                                                                            }
                                                                        },
                                                                        observer.OnError,
                                                                        observer.OnCompleted);
                                                                });

    /// <summary>
    /// Executes the CreateUpdateChanges operation.
    /// </summary>
    /// <param name="prev">The prev value.</param>
    /// <param name="curr">The curr value.</param>
    /// <returns>The result of the operation.</returns>
    private Change<TObject, TKey>[] CreateUpdateChanges(in ValueContainer prev, in ValueContainer curr)
    {
        if (EqualityComparer<TKey>.Default.Equals(prev.Key, curr.Key))
        {
            // Key is the same, so Update (unless values are equal)
            if (!_equalityComparer.Equals(prev.Object, curr.Object))
            {
                return [new Change<TObject, TKey>(ChangeReason.Update, curr.Key, curr.Object, prev.Object)];
            }

            return [];
        }

        // Key Change means Remove/Add
        return
        [
            new Change<TObject, TKey>(ChangeReason.Remove, prev.Key, prev.Object),
            new Change<TObject, TKey>(ChangeReason.Add, curr.Key, curr.Object)
        ];
    }

/// <summary>
/// Represents the ValueContainer value.
/// </summary>
/// <param name="obj">The obj value.</param>
/// <param name="key">The key value.</param>
private readonly struct ValueContainer(TObject obj, TKey key)
    {
        /// <summary>
        /// Gets the Object value.
        /// </summary>
        public TObject Object { get; } = obj;

        /// <summary>
        /// Gets the Key value.
        /// </summary>
        public TKey Key { get; } = key;
    }
}
