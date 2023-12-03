// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Linq;
using DynamicData.Kernel;

namespace DynamicData.Cache.Internal;

internal sealed class EditDiffChangeSetOptional<TObject, TKey>(IObservable<Optional<TObject>> source, Func<TObject, TKey> keySelector, IEqualityComparer<TObject>? equalityComparer)
    where TObject : notnull
    where TKey : notnull
{
    private readonly IObservable<Optional<TObject>> _source = source ?? throw new ArgumentNullException(nameof(source));

    private readonly IEqualityComparer<TObject> _equalityComparer = equalityComparer ?? EqualityComparer<TObject>.Default;

    private readonly Func<TObject, TKey> _keySelector = keySelector ?? throw new ArgumentNullException(nameof(keySelector));

    public IObservable<IChangeSet<TObject, TKey>> Run() => Observable.Create<IChangeSet<TObject, TKey>>(observer =>
                                                                {
                                                                    var previous = Optional.None<ValueContainer>();

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

    private readonly struct ValueContainer(TObject obj, TKey key)
    {
        public TObject Object { get; } = obj;

        public TKey Key { get; } = key;
    }
}
