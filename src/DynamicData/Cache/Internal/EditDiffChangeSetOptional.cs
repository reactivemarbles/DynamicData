// Copyright (c) 2011-2023 Roland Pheasant. All rights reserved.
// Roland Pheasant licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reactive.Disposables;
using System.Reactive.Linq;
using DynamicData.Kernel;

namespace DynamicData.Cache.Internal;

internal sealed class EditDiffChangeSetOptional<TObject, TKey>
    where TObject : notnull
    where TKey : notnull
{
    private static readonly IEqualityComparer<TKey> KeyComparer = EqualityComparer<TKey>.Default;

    private readonly IObservable<Optional<TObject>> _source;

    private readonly IEqualityComparer<TObject> _equalityComparer;

    private readonly Func<TObject, TKey> _keySelector;

    public EditDiffChangeSetOptional(IObservable<Optional<TObject>> source, Func<TObject, TKey> keySelector, IEqualityComparer<TObject>? equalityComparer)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _keySelector = keySelector ?? throw new ArgumentNullException(nameof(keySelector));
        _equalityComparer = equalityComparer ?? EqualityComparer<TObject>.Default;
    }

    public IObservable<IChangeSet<TObject, TKey>> Run()
    {
        return Observable.Create<IChangeSet<TObject, TKey>>(observer =>
        {
            var shared = _source.StartWith(Optional.None<TObject>()).Synchronize().Publish();

            var cleanup = shared.Zip(shared.Skip(1)).Select(
                tuple =>
                {
                    var previous = tuple.First;
                    var current = tuple.Second;

                    if (previous.HasValue && current.HasValue)
                    {
                        var previousKey = _keySelector(previous.Value);
                        var currentKey = _keySelector(current.Value);

                        if (KeyComparer.Equals(previousKey, currentKey))
                        {
                            if (!_equalityComparer.Equals(previous.Value, current.Value))
                            {
                                return new[] { new Change<TObject, TKey>(ChangeReason.Update, currentKey, current.Value, previous.Value) };
                            }
                        }
                        else
                        {
                            return new[] { new Change<TObject, TKey>(ChangeReason.Remove, previousKey, previous.Value), new Change<TObject, TKey>(ChangeReason.Add, currentKey, current.Value) };
                        }
                    }
                    else if (previous.HasValue)
                    {
                        var previousKey = _keySelector(previous.Value);
                        return new[] { new Change<TObject, TKey>(ChangeReason.Remove, previousKey, previous.Value) };
                    }
                    else if (current.HasValue)
                    {
                        var currentKey = _keySelector(current.Value);
                        return new[] { new Change<TObject, TKey>(ChangeReason.Add, currentKey, current.Value) };
                    }

                    return Array.Empty<Change<TObject, TKey>>();
                })
                .Where(changes => changes.Length > 0)
                .Select(changes => new ChangeSet<TObject, TKey>(changes))
                .SubscribeSafe(observer);

            return new CompositeDisposable(cleanup, shared.Connect());
        });
    }
}
